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
using Tracker.Domain.Vehiculos;                // IVehiculoRepository, IAsignacionDispositivoRepository
using Tracker.Domain.Viajes;                   // IViajeRepository

namespace Tracker.Worker.Infrastructure.Services
{
    public sealed class PorticoDetectionService : IPorticoDetectionService
    {
        private readonly IPorticoRepository _porticos;
        private readonly ITransitoRepository _transitos;
        private readonly ITarifaPorticoRepository _tarifas;
        private readonly IBandaHorarioRepository _bandas;
        private readonly ICalendarioChile _calendario;
        private readonly IAsignacionDispositivoRepository _asignaciones;
        private readonly IVehiculoRepository _vehiculos;
        private readonly IViajeRepository _viajes;
        private readonly IUltimaPosicionCache _ultimaPosicion;
        private readonly IUnitOfWork _uow; // si no usas UoW, reemplaza por save en capa superior
        private readonly GeometryFactory _gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // Como no existen en Portico, uso constantes locales
        private const double RADIO_M = 50.0;          // radio de captura
        private const double TOL_ANGULO = 45.0;       // tolerancia de heading
        private static readonly TimeSpan VENTANA = TimeSpan.FromSeconds(90); // de-bounce

        // Límites para unir dos fixes en un tramo. Si el device estuvo sin señal,
        // el "segmento" sería una recta de kilómetros que atraviesa pórticos por
        // los que nunca se pasó: en ese caso es más honesto no detectar nada.
        private static readonly TimeSpan MAX_HUECO = TimeSpan.FromSeconds(60);
        private const double MAX_TRAMO_M = 3000.0;

        public PorticoDetectionService(
            IPorticoRepository porticos,
            ITransitoRepository transitos,
            ITarifaPorticoRepository tarifas,
            IBandaHorarioRepository bandas,
            ICalendarioChile calendario,
            IAsignacionDispositivoRepository asignaciones,
            IVehiculoRepository vehiculos,
            IViajeRepository viajes,
            IUltimaPosicionCache ultimaPosicion,
            IUnitOfWork uow)
        {
            _porticos = porticos;
            _transitos = transitos;
            _tarifas = tarifas;
            _bandas = bandas;
            _calendario = calendario;
            _asignaciones = asignaciones;
            _vehiculos = vehiculos;
            _viajes = viajes;
            _ultimaPosicion = ultimaPosicion;
            _uow = uow;
        }

        public async Task<TransitoDetectadoDto?> DetectarYGuardarAsync(GpsEventDto evt, KafkaMetaDto meta, CancellationToken ct)
        {
            // 1) Punto GPS (lon, lat) SRID 4326
            var punto = _gf.CreatePoint(new Coordinate(evt.Lon, evt.Lat));
            var ts = evt.Utc;

            // 2) Tramo recorrido desde el fix anterior. Detectar por segmento y no
            //    por punto es lo que evita saltarse pórticos: a 100 km/h se avanzan
            //    ~55 m entre muestras y el radio de captura son 50 m, así que es
            //    perfectamente posible que ninguna muestra caiga dentro.
            var anterior = _ultimaPosicion.Obtener(evt.DeviceId);
            _ultimaPosicion.Guardar(evt.DeviceId, new UltimaPosicion(evt.Lat, evt.Lon, ts));

            LineString? tramo = null;
            double? headingTramo = null;

            if (anterior is not null)
            {
                var hueco = ts - anterior.Utc;
                var avanceM = DistanciaMetros(anterior.Lat, anterior.Lon, evt.Lat, evt.Lon);

                // > 0 descarta eventos desordenados o repetidos; avanceM > 0 evita
                // una línea degenerada (device quieto), que SQL Server rechaza.
                if (hueco > TimeSpan.Zero && hueco <= MAX_HUECO && avanceM > 0 && avanceM <= MAX_TRAMO_M)
                {
                    tramo = _gf.CreateLineString(new[]
                    {
                        new Coordinate(anterior.Lon, anterior.Lat),
                        new Coordinate(evt.Lon, evt.Lat),
                    });
                    headingTramo = Bearing(anterior.Lat, anterior.Lon, evt.Lat, evt.Lon);
                }
            }

            // Candidatos cercanos (ordenados por distancia en BD). Sin tramo válido
            // se cae al modo antiguo por punto, que sigue siendo correcto.
            var candidatos = tramo is not null
                ? await _porticos.GetNearLineAsync(tramo, RADIO_M, take: 5, ct: ct)
                : await _porticos.GetNearAsync(punto, RADIO_M, take: 5, ct: ct);

            if (candidatos.Count == 0)
                return null;

            // El heading del tramo suple al del GPS: al ir despacio o parado, Android
            // e iOS mandan heading nulo o basura, y sin él el filtro de corredor no
            // se puede aplicar.
            var headingEfectivo = evt.HeadingDeg ?? headingTramo;

            // 3) Recorre candidatos; valida heading solo si hay corredor y hay heading
            foreach (var portico in candidatos)
            {
                if (headingEfectivo is double heading && portico.Corredor is not null)
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

                // 5) Atribución: ¿de qué vehículo era este device EN ESE INSTANTE?
                //    Se resuelve por la asignación vigente al momento del paso,
                //    no por la asignación de hoy: así reasignar el teléfono a otro
                //    auto no reescribe los cobros pasados.
                Guid? vehiculoId = null;
                var categoria = VehicleCategory.C1;   // fallback si el device no está asignado

                // DeviceId es no-nulable en el DTO; si llega vacío desde Protobuf,
                // las consultas simplemente no encuentran nada y el tránsito se
                // registra sin atribuir.
                var asignacion = await _asignaciones.GetVigenteAsync(evt.DeviceId, ts, ct);

                if (asignacion is not null)
                {
                    vehiculoId = asignacion.VehiculoId;
                    var vehiculo = await _vehiculos.GetByIdAsync(asignacion.VehiculoId, ct);
                    if (vehiculo is not null)
                        categoria = vehiculo.Categoria;   // la categoría real manda sobre el C1 fijo
                }

                // Viaje en curso del device, si lo hay. Un tránsito fuera de viaje
                // se registra igual: el peaje existe aunque el conductor no haya
                // apretado "Iniciar".
                var viaje = await _viajes.GetEnCursoPorDeviceAsync(evt.DeviceId, ct);

                // 6) Resolver banda según la hora local Chile del tránsito y la
                //    grilla horaria del pórtico; luego tarifa vigente y precio.
                var diaTipo = _calendario.DiaTipoDe(ts);
                var local = _calendario.ToLocal(ts);
                var horaLocal = TimeOnly.FromDateTime(local);
                var banda = await _bandas.ResolverBandaAsync(portico.Id, diaTipo, horaLocal, ct);

                var tarifa = await _tarifas.GetVigenteAsync(portico.Id, categoria, banda, ts, ct);
                var precio = CalcularPrecio(tarifa, portico.LongitudKm);

                // 7) Guardar el tránsito con toda la evidencia del cobro
                var transito = new Transito
                {
                    Id = Guid.NewGuid(),
                    PorticoId = portico.Id,
                    DeviceId = evt.DeviceId,
                    VehiculoId = vehiculoId,
                    ViajeId = viaje?.Id,
                    Utc = ts,
                    Posicion = punto,

                    // Fecha/hora local persistidas: el reporte diario agrupa por
                    // el día de Chile, no por el día UTC.
                    FechaLocal = DateOnly.FromDateTime(local),
                    HoraLocal = horaLocal,
                    DiaTipo = diaTipo,

                    Categoria = categoria,
                    Banda = banda,

                    // Trazabilidad del monto: qué tarifa se aplicó.
                    TarifaPorticoId = tarifa?.Id,

                    // Snapshots del catálogo, congelados al momento del paso.
                    AutopistaSnapshot = portico.Autopista,
                    PorticoCodigoSnapshot = portico.Codigo,
                    SentidoSnapshot = portico.Sentido,

                    EstadoConciliacion = EstadoConciliacion.Pendiente,

                    // opcionales pero útiles si existen en tu entidad
                    ExactitudM = (evt.AccuracyM ?? 0),   // double
                    Fuente = "gps",                  // string (topic/fuente)
                    PrecioCalculado = precio
                };

                await _transitos.AddAsync(transito, ct);

                // Totales del viaje al vuelo, para que la lista de viajes no tenga
                // que agregar sobre los tránsitos en cada request. Al cerrar el
                // viaje se recalculan contra la BD por si esto quedó corto.
                if (viaje is not null)
                {
                    viaje.CantidadTransitos += 1;
                    viaje.TotalGasto += precio;
                }

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

        /// <summary>Distancia haversine en metros. Sólo para acotar el tramo.</summary>
        private static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
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
