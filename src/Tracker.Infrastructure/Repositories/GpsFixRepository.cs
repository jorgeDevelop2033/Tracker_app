using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{
    public sealed class GpsFixRepository : IGpsFixRepository
    {
        private readonly TrackerDbContext _db;
        private readonly GeometryFactory _geo;
        private readonly ILogger<GpsFixRepository> _log;

        public GpsFixRepository(TrackerDbContext db, ILogger<GpsFixRepository> log)
        {
            _db = db;
            _log = log;
            _geo = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326); // SQL Server geography SRID
        }

        // ---------- Ingesta / idempotencia ----------
        public Task<bool> ExistsKafkaOffsetAsync(string topic, int partition, long offset, CancellationToken ct = default)
            => _db.GpsFixes.AsNoTracking().AnyAsync(x =>
                   x.KafkaTopic == topic && x.KafkaPartition == partition && x.KafkaOffset == offset, ct);

        public async Task AddAsync(GpsFix entity, CancellationToken ct = default)
        {
            _db.GpsFixes.Add(entity);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueKafkaOffsetViolation(ex))
            {
                _log.LogWarning("Offset duplicado (benigno): {Topic}[{Partition}]@{Offset}",
                    entity.KafkaTopic, entity.KafkaPartition, entity.KafkaOffset);
            }
        }

        public async Task AddRangeAsync(IEnumerable<GpsFix> entities, CancellationToken ct = default)
        {
            _db.GpsFixes.AddRange(entities);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueKafkaOffsetViolation(ex))
            {
                _log.LogWarning("Offsets duplicados detectados en batch. Reintentables/benignos.");
            }
        }

        private static bool IsUniqueKafkaOffsetViolation(DbUpdateException ex)
            => ex.InnerException?.Message.Contains("ux_gpsfix_kafka_position", StringComparison.OrdinalIgnoreCase) == true;

        // ---------- Lecturas ----------
        public Task<GpsFix?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.GpsFixes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task<GpsFix?> GetLastByDeviceAsync(string deviceId, CancellationToken ct = default)
            => _db.GpsFixes.AsNoTracking()
                .Where(x => x.DeviceId == deviceId)
                .OrderByDescending(x => x.Utc)
                .FirstOrDefaultAsync(ct);

        public Task<List<GpsFix>> ListByDeviceAndUtcRangeAsync(
            string deviceId, DateTime fromUtc, DateTime toUtc, int take = 1000, CancellationToken ct = default)
            => _db.GpsFixes.AsNoTracking()
                .Where(x => x.DeviceId == deviceId && x.Utc >= fromUtc && x.Utc <= toUtc)
                .OrderBy(x => x.Utc)
                .Take(Math.Clamp(take, 1, 10000))
                .ToListAsync(ct);

        // ---------- Espacial ----------
        // Nota: con SQL Server geography, la distancia se evalúa en metros.
        public Task<List<GpsFix>> ListByViajeAsync(
            Guid viajeId, int take = 50_000, CancellationToken ct = default)
            => _db.GpsFixes.AsNoTracking()
                  .Where(x => x.ViajeId == viajeId)
                  .OrderBy(x => x.Utc)
                  .Take(Math.Clamp(take, 1, 200_000))
                  .ToListAsync(ct);

        public Task<List<GpsFix>> ListWithinRadiusAsync(
            double lat, double lon, double radiusMeters, string? deviceIdFilter = null, int take = 500, CancellationToken ct = default)
        {
            if (radiusMeters <= 0) throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            var center = _geo.CreatePoint(new Coordinate(lon, lat)); // NTS: (lon, lat)

            var q = _db.GpsFixes.AsNoTracking()
                .Where(x => x.Location != null && x.Location.Distance(center) <= radiusMeters);

            if (!string.IsNullOrWhiteSpace(deviceIdFilter))
                q = q.Where(x => x.DeviceId == deviceIdFilter);

            return q.OrderByDescending(x => x.Utc)
                    .Take(Math.Clamp(take, 1, 5000))
                    .ToListAsync(ct);
        }

        // ---------- Mantenimiento ----------
        /// <summary>
        /// Purga fixes antiguos de un device en lotes.
        /// <para>
        /// Va por tandas y no de una sola vez a propósito: <c>gps_fix</c> crece a
        /// millones de filas, y un DELETE único de ese tamaño escala el bloqueo a
        /// toda la tabla, infla el log de transacciones y puede tumbar al Worker.
        /// Con <c>ExecuteDelete</c> tampoco se materializan entidades en memoria
        /// (la versión anterior traía todas las filas al proceso antes de borrar).
        /// </para>
        /// </summary>
        public async Task<int> DeleteByDeviceBeforeUtcAsync(string deviceId, DateTime beforeUtc, CancellationToken ct = default)
        {
            const int loteSize = 5_000;
            var total = 0;

            while (!ct.IsCancellationRequested)
            {
                var borradas = await _db.GpsFixes
                    .Where(x => x.DeviceId == deviceId && x.Utc < beforeUtc)
                    .OrderBy(x => x.Utc)
                    .Take(loteSize)
                    .ExecuteDeleteAsync(ct);

                total += borradas;
                if (borradas < loteSize) break;
            }

            if (total > 0)
                _log.LogInformation("Purga gps_fix: {Filas} filas de {Device} anteriores a {Corte:u}.",
                    total, deviceId, beforeUtc);

            return total;
        }
    }
}
