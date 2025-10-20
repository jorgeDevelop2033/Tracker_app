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
        public async Task<int> DeleteByDeviceBeforeUtcAsync(string deviceId, DateTime beforeUtc, CancellationToken ct = default)
        {
            var old = await _db.GpsFixes
                .Where(x => x.DeviceId == deviceId && x.Utc < beforeUtc)
                .ToListAsync(ct);

            if (old.Count == 0) return 0;

            _db.GpsFixes.RemoveRange(old);
            return await _db.SaveChangesAsync(ct);
        }
    }
}
