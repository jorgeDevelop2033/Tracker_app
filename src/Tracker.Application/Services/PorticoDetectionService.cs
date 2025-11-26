// Tracker.Worker.Infrastructure/Services/PorticoDetectionService.cs
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Tracker.Domain.Porticos;                 // IPorticoRepository
using Tracker.Domain.Transitos;               // ITransitoRepository
using Tracker.Domain.Entities;                // Transito, Portico
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
        private readonly IUnitOfWork _uow; // si no usas UoW, reemplaza por save en capa superior
        private readonly GeometryFactory _gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // Como no existen en Portico, uso constantes locales
        private const double RADIO_M = 50.0;          // radio de captura
        private const double TOL_ANGULO = 45.0;       // tolerancia de heading
        private static readonly TimeSpan VENTANA = TimeSpan.FromSeconds(90); // de-bounce

        public PorticoDetectionService(
            IPorticoRepository porticos,
            ITransitoRepository transitos,
            IUnitOfWork uow)
        {
            _porticos = porticos;
            _transitos = transitos;
            _uow = uow;
        }

        public async Task DetectarYGuardarAsync(GpsEventDto evt, KafkaMetaDto meta, CancellationToken ct)
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
                return;

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

                // 5) Guardar Transito con los campos que SÍ existen en tu entidad
                var transito = new Transito
                {
                    Id = Guid.NewGuid(),
                    PorticoId = portico.Id,
                    Utc = ts,
                    Posicion = punto,

                    Categoria = VehicleCategory.C1,   // o la que corresponda en tu enum

                    Banda = Banda.TBP,                // o Banda.Diurna/Nocturna según tu modelo

                    // opcionales pero útiles si existen en tu entidad
                    ExactitudM = (evt.AccuracyM ?? 0),   // double
                    Fuente = "gps",                  // string (topic/fuente)
                    PrecioCalculado = 0m                      // decimal; lo puedes recalcular luego
                };

                await _transitos.AddAsync(transito, ct);
                await _uow.SaveChangesAsync(ct); // si no usas UoW, mueve el commit donde corresponda
                return; // registramos el primer match válido y salimos
            }
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
