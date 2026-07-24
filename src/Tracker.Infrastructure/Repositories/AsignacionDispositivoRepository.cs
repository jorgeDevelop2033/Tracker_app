#nullable enable
using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;
using Tracker.Domain.Vehiculos;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{
    public sealed class AsignacionDispositivoRepository : IAsignacionDispositivoRepository
    {
        private readonly TrackerDbContext _db;

        public AsignacionDispositivoRepository(TrackerDbContext db) => _db = db;

        public Task<AsignacionDispositivo?> GetVigenteAsync(string deviceId, DateTime utc, CancellationToken ct = default)
            => _db.AsignacionesDispositivo.AsNoTracking()
                  .Where(a => a.DeviceId == deviceId
                           && a.DesdeUtc <= utc
                           && (a.HastaUtc == null || utc < a.HastaUtc))
                  // Si por corrupción hubiera solapamiento, gana la más reciente.
                  .OrderByDescending(a => a.DesdeUtc)
                  .FirstOrDefaultAsync(ct);

        public Task<AsignacionDispositivo?> GetAbiertaAsync(string deviceId, CancellationToken ct = default)
            => _db.AsignacionesDispositivo
                  .FirstOrDefaultAsync(a => a.DeviceId == deviceId && a.HastaUtc == null, ct);

        public Task<List<AsignacionDispositivo>> ListByVehiculoAsync(Guid vehiculoId, CancellationToken ct = default)
            => _db.AsignacionesDispositivo.AsNoTracking()
                  .Where(a => a.VehiculoId == vehiculoId)
                  .OrderByDescending(a => a.DesdeUtc)
                  .ToListAsync(ct);

        public async Task AddAsync(AsignacionDispositivo asignacion, CancellationToken ct = default)
            => await _db.AsignacionesDispositivo.AddAsync(asignacion, ct);
    }
}
