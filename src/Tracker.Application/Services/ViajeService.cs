#nullable enable
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using Tracker.Contracts.Enums;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;
using Tracker.Domain.Vehiculos;
using Tracker.Domain.Viajes;

namespace Tracker.Application.Services
{
    public sealed class ViajeService : IViajeService
    {
        private readonly IViajeRepository _viajes;
        private readonly IAsignacionDispositivoRepository _asignaciones;
        private readonly IGpsFixRepository _fixes;
        private readonly ICalendarioChile _calendario;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ViajeService> _log;

        private readonly GeometryFactory _gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        /// <summary>
        /// Tolerancia de Douglas-Peucker en grados (~10 m). Con esto un viaje
        /// urbano de miles de puntos baja a unos cientos sin que la ruta se note
        /// distinta en un mapa.
        /// </summary>
        private const double TOLERANCIA_SIMPLIFICACION = 0.0001;

        public ViajeService(
            IViajeRepository viajes,
            IAsignacionDispositivoRepository asignaciones,
            IGpsFixRepository fixes,
            ICalendarioChile calendario,
            IUnitOfWork uow,
            ILogger<ViajeService> log)
        {
            _viajes = viajes;
            _asignaciones = asignaciones;
            _fixes = fixes;
            _calendario = calendario;
            _uow = uow;
            _log = log;
        }

        public async Task<Viaje> IniciarAsync(
            string deviceId, Guid? vehiculoId, string? nombre, DateTime utc, CancellationToken ct = default)
        {
            // Un device no puede tener dos viajes vivos: si el conductor olvidó
            // detener el anterior, lo cerramos aquí en vez de dejar dos abiertos
            // compitiendo por los mismos tránsitos.
            var abierto = await _viajes.GetEnCursoPorDeviceAsync(deviceId, ct);
            if (abierto is not null)
            {
                _log.LogInformation("Viaje {Id} seguía abierto para {Device}; se cierra al iniciar uno nuevo.",
                    abierto.Id, deviceId);
                await CerrarAsync(abierto, utc, EstadoViaje.CerradoPorInactividad, ct);
            }

            var vehiculo = vehiculoId;
            if (vehiculo is null)
            {
                var asignacion = await _asignaciones.GetVigenteAsync(deviceId, utc, ct)
                    ?? throw new InvalidOperationException(
                        $"El dispositivo '{deviceId}' no tiene un vehículo asignado en {utc:u}. " +
                        "Asigna el dispositivo antes de iniciar un viaje.");
                vehiculo = asignacion.VehiculoId;
            }

            var viaje = new Viaje
            {
                Id = Guid.NewGuid(),
                VehiculoId = vehiculo.Value,
                DeviceId = deviceId,
                InicioUtc = utc,
                FechaLocalInicio = DateOnly.FromDateTime(_calendario.ToLocal(utc)),
                Estado = EstadoViaje.EnCurso,
                Nombre = nombre
            };

            await _viajes.AddAsync(viaje, ct);
            await _uow.SaveChangesAsync(ct);

            _log.LogInformation("Viaje {Id} iniciado para vehículo {Vehiculo} (device {Device}).",
                viaje.Id, viaje.VehiculoId, deviceId);

            return viaje;
        }

        public async Task<Viaje?> FinalizarAsync(
            Guid viajeId, DateTime utc, EstadoViaje estadoFinal, CancellationToken ct = default)
        {
            var viaje = await _viajes.GetByIdAsync(viajeId, ct);
            if (viaje is null) return null;

            // Idempotente: cerrar un viaje ya cerrado no lo altera ni falla.
            if (viaje.Estado != EstadoViaje.EnCurso) return viaje;

            await CerrarAsync(viaje, utc, estadoFinal, ct);
            await _uow.SaveChangesAsync(ct);
            return viaje;
        }

        public Task<Viaje?> ObtenerEnCursoAsync(string deviceId, CancellationToken ct = default)
            => _viajes.GetEnCursoPorDeviceAsync(deviceId, ct);

        /// <summary>
        /// Consolida el viaje en memoria (sin guardar): totales, recorrido y punto
        /// final. El SaveChanges queda en manos del llamador para poder encadenar
        /// varios cierres en una sola transacción.
        /// </summary>
        private async Task CerrarAsync(Viaje viaje, DateTime utc, EstadoViaje estadoFinal, CancellationToken ct)
        {
            viaje.Estado = estadoFinal;

            // Totales autoritativos desde la BD: el contador incremental que
            // lleva el detector puede haber derivado si algo falló a medio camino.
            var (cantidad, total) = await _viajes.ResumenTransitosAsync(viaje.Id, ct);
            viaje.CantidadTransitos = cantidad;
            viaje.TotalGasto = total;

            var fixes = await _fixes.ListByViajeAsync(viaje.Id, ct: ct);
            viaje.CantidadFixes = fixes.Count;

            if (fixes.Count > 0)
            {
                viaje.PuntoInicio ??= _gf.CreatePoint(new Coordinate(fixes[0].Lon, fixes[0].Lat));
                var ultimo = fixes[^1];
                viaje.PuntoFin = _gf.CreatePoint(new Coordinate(ultimo.Lon, ultimo.Lat));
                viaje.DistanciaKm = DistanciaKm(fixes);
                viaje.RutaSimplificada = Simplificar(fixes);
            }

            // Cuando lo cierra el conductor, la hora que manda es la suya. Cuando
            // lo cierra el job de inactividad, el viaje realmente terminó cuando
            // dejó de emitir, no cuando lo detectamos: usar "ahora" inflaría la
            // duración con todo el tiempo que estuvo colgado.
            viaje.FinUtc = estadoFinal == EstadoViaje.CerradoPorInactividad && fixes.Count > 0
                ? fixes[^1].Utc
                : utc;

            _log.LogInformation(
                "Viaje {Id} cerrado ({Estado}): {Transitos} tránsitos, ${Total}, {Km:F1} km, {Fixes} fixes → {Puntos} puntos de ruta.",
                viaje.Id, estadoFinal, cantidad, total, viaje.DistanciaKm, fixes.Count,
                viaje.RutaSimplificada?.NumPoints ?? 0);
        }

        /// <summary>
        /// Comprime el recorrido con Douglas-Peucker. Es lo que permite purgar los
        /// fixes crudos más adelante conservando la forma del viaje.
        /// </summary>
        private LineString? Simplificar(IReadOnlyList<GpsFix> fixes)
        {
            // Un LineString necesita al menos dos vértices distintos.
            var coords = fixes
                .Select(f => new Coordinate(f.Lon, f.Lat))
                .Where(c => !double.IsNaN(c.X) && !double.IsNaN(c.Y))
                .ToList();

            if (coords.Count < 2) return null;

            var linea = _gf.CreateLineString(coords.ToArray());
            var simplificada = DouglasPeuckerSimplifier.Simplify(linea, TOLERANCIA_SIMPLIFICACION);

            // La simplificación puede degenerar la geometría si todos los puntos
            // caen dentro de la tolerancia (vehículo detenido todo el viaje).
            return simplificada as LineString ?? linea;
        }

        /// <summary>Distancia recorrida sumando haversine entre fixes consecutivos.</summary>
        private static double DistanciaKm(IReadOnlyList<GpsFix> fixes)
        {
            const double R = 6371.0;
            var total = 0.0;

            for (var i = 1; i < fixes.Count; i++)
            {
                var a = fixes[i - 1];
                var b = fixes[i];

                var dLat = Rad(b.Lat - a.Lat);
                var dLon = Rad(b.Lon - a.Lon);
                var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                      + Math.Cos(Rad(a.Lat)) * Math.Cos(Rad(b.Lat))
                      * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

                total += 2 * R * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
            }

            return total;
        }

        private static double Rad(double grados) => grados * Math.PI / 180.0;
    }
}
