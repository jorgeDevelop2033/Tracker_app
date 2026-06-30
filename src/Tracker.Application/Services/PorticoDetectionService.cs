// Tracker.Worker.Infrastructure/Services/PorticoDetectionService.cs
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Tracker.Domain.Porticos;                 // IPorticoRepository
using Tracker.Domain.Transitos;               // ITransitoRepository
using Tracker.Domain.Tarifas;                 // ITarifaPorticoRepository
using Tracker.Domain.Entities;                // Transito, Portico, TarifaPortico
using Tracker.Domain.Abstractions;            // IUnitOfWork (ajusta si vive en otro ns)
using Tracker.Domain.Abstractions.Filter;
using Tracker.Application.Dtos;
using Tracker.Application.Services;
using Tracker.Contracts.Enums;

namespace Tracker.Worker.Infrastructure.Services
{
    public sealed class PorticoDetectionService : IPorticoDetectionService
    {
        private readonly IPorticoRepository _porticos;
        private readonly ITransitoRepository _transitos;
        private readonly ITarifaPorticoRepository _tarifas;
        private readonly IBandaHorarioRepository _bandas;
        private readonly ICalendarioChile _calendario;
        private readonly IUnitOfWork _uow; // si no usas UoW, reemplaza por save en capa superior
        private readonly GeometryFactory _gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // Como no existen en Portico, uso constantes locales
        private const double RADIO_M = 50.0;          // radio de captura
        private const double TOL_ANGULO = 45.0;       // tolerancia de heading
        private static readonly TimeSpan VENTANA = TimeSpan.FromSeconds(90); // de-bounce

        public PorticoDetectionService(
            IPorticoRepository porticos,
            ITransitoRepository transitos,
            ITarifaPorticoRepository tarifas,
            IBandaHorarioRepository bandas,
            ICalendarioChile calendario,
            IUnitOfWork uow)
        {
            _porticos = porticos;
            _transitos = transitos;
            _tarifas = tarifas;
            _bandas = bandas;
            _calendario = calendario;
            _uow = uow;
        }

        public async Task<TransitoDetectadoDto?> DetectarYGuardarAsync(GpsEventDto evt, KafkaMetaDto meta, CancellationToken ct)
        {
            // 1) Punto GPS (lon, lat) SRID 4326
            var punto = _gf.CreatePoint(new Coordinate(evt.Lon, evt.Lat));
            var ts = evt.Utc;

            // 2) Candidatos cercanos por punto (ordenados por distancia en BD)
            var candidatos = await _porticos.GetNearAsync(
                position4326: punto,
                maxDistanceMeters: RADIO_M,
                take: 5,
                ct: ct);

            if (candidatos.Count == 0)
                return null;

            // 3) Recorre candidatos; valida heading solo si hay corredor y viene heading
            foreach (var portico in candidatos)
            {
                if (evt.HeadingDeg is double heading && portico.Corredor is not null)
                {
                    var bearing = BearingFromLine(portico.Corredor);
                    var diff = AngularDiff(heading, bearing);
                    if (diff > TOL_ANGULO) continue;
                }

                // 4) De-bounce temporal (usa tu repo: GetByPorticoAsync)
                var desde = ts - VENTANA;
                var hasta = ts + VENTANA;

                // PageSize=1 para hacer existencia O(1)
                var page = new Pagination(1, 1);
                var recientes = await _transitos.GetByPorticoAsync(portico.Id, desde, hasta, page, ct);
                if (recientes.Total > 0)
                    continue;

                // 5) Resolver banda según la hora local Chile del tránsito y la
                //    grilla horaria del pórtico; luego tarifa vigente y precio.
                //    (Categoría fija C1 por ahora; Fase 2 derivará del vehículo.)
                var categoria = VehicleCategory.C1;

                var diaTipo = _calendario.DiaTipoDe(ts);
                var horaLocal = TimeOnly.FromDateTime(_calendario.ToLocal(ts));
                var banda = await _bandas.ResolverBandaAsync(portico.Id, diaTipo, horaLocal, ct);

                var tarifa = await _tarifas.GetVigenteAsync(portico.Id, categoria, banda, ts, ct);
                var precio = CalcularPrecio(tarifa, portico.LongitudKm);

                // 6) Guardar Transito con los campos que SÍ existen en tu entidad
                var transito = new Transito
                {
                    Id = Guid.NewGuid(),
                    PorticoId = portico.Id,
                    DeviceId = evt.DeviceId,
                    Utc = ts,
                    Posicion = punto,

                    Categoria = categoria,
                    Banda = banda,

                    // opcionales pero útiles si existen en tu entidad
                    ExactitudM = (evt.AccuracyM ?? 0),   // double
                    Fuente = "gps",                  // string (topic/fuente)
                    PrecioCalculado = precio
                };

                await _transitos.AddAsync(transito, ct);
                await _uow.SaveChangesAsync(ct); // si no usas UoW, mueve el commit donde corresponda

                // Registramos el primer match válido y devolvemos el resultado
                // para notificarlo en vivo al dashboard.
                return new TransitoDetectadoDto(
                    DeviceId: evt.DeviceId,
                    PorticoId: portico.Id,
                    PorticoCodigo: portico.Codigo,
                    Autopista: portico.Autopista,
                    Precio: precio,
                    Lat: evt.Lat,
                    Lon: evt.Lon,
                    Utc: ts);
            }

            return null; // ningún candidato pasó los filtros
        }

        // --- cálculo de tarifa ---

        /// <summary>
        /// Precio del tránsito según la tarifa vigente:
        ///  - si trae <see cref="TarifaPortico.ValorFijo"/>, ése es el precio;
        ///  - si trae <see cref="TarifaPortico.ValorPorKm"/>, se multiplica por la
        ///    longitud (snapshot de la tarifa o, en su defecto, la del pórtico);
        ///  - si no hay tarifa vigente, 0 (el tránsito se registra igual).
        /// </summary>
        private static decimal CalcularPrecio(TarifaPortico? tarifa, decimal? longitudPortico)
        {
            if (tarifa is null) return 0m;

            if (tarifa.ValorFijo is decimal fijo)
                return fijo;

            if (tarifa.ValorPorKm is decimal porKm)
            {
                var km = tarifa.LongitudKmSnapshot ?? longitudPortico ?? 0m;
                return porKm * km;
            }

            return 0m;
        }

        // --- utilidades de heading ---

        private static double BearingFromLine(LineString ls)
        {
            if (ls.NumPoints < 2) return 0;
            var a = ls.GetCoordinateN(0);
            var b = ls.GetCoordinateN(1);
            return Bearing(a.Y, a.X, b.Y, b.X); // (latA, lonA, latB, lonB)
        }

        private static double Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            double dLon = Deg2Rad(lon2 - lon1);
            lat1 = Deg2Rad(lat1); lat2 = Deg2Rad(lat2);
            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(dLon) - Math.Sin(lat1) * Math.Sin(lat2);
            double brng = Math.Atan2(y, x);
            return (Rad2Deg(brng) + 360.0) % 360.0;
        }

        private static double AngularDiff(double a, double b)
        {
            double diff = Math.Abs(a - b) % 360.0;
            return diff > 180.0 ? 360.0 - diff : diff;
        }

        private static double Deg2Rad(double d) => d * Math.PI / 180.0;
        private static double Rad2Deg(double r) => r * 180.0 / Math.PI;
    }
}
