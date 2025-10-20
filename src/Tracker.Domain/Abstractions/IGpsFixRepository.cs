using Tracker.Domain.Entities;

namespace Tracker.Domain.Abstractions
{
    public interface IGpsFixRepository
    {
        // Ingesta / idempotencia
        Task<bool> ExistsKafkaOffsetAsync(string topic, int partition, long offset, CancellationToken ct = default);
        Task AddAsync(GpsFix entity, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<GpsFix> entities, CancellationToken ct = default);

        // Lecturas
        Task<GpsFix?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<GpsFix?> GetLastByDeviceAsync(string deviceId, CancellationToken ct = default);
        Task<List<GpsFix>> ListByDeviceAndUtcRangeAsync(string deviceId, DateTime fromUtc, DateTime toUtc, int take = 1000, CancellationToken ct = default);

        // Consultas espaciales (metros)
        Task<List<GpsFix>> ListWithinRadiusAsync(double lat, double lon, double radiusMeters, string? deviceIdFilter = null, int take = 500, CancellationToken ct = default);

        // Mantenimiento
        Task<int> DeleteByDeviceBeforeUtcAsync(string deviceId, DateTime beforeUtc, CancellationToken ct = default);
    }
}
