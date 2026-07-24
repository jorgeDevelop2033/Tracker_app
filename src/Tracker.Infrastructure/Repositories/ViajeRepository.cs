#nullable enable
using Microsoft.EntityFrameworkCore;
using Tracker.Contracts.Enums;
using Tracker.Domain.Entities;
using Tracker.Domain.Viajes;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{
    public sealed class ViajeRepository : IViajeRepository
    {
        private readonly TrackerDbContext _db;

        public ViajeRepository(TrackerDbContext db) => _db = db;

        public Task<Viaje?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.Viajes.FirstOrDefaultAsync(v => v.Id == id, ct);

        // Sin AsNoTracking: quien resuelve el viaje en curso normalmente va a
        // actualizar sus totales en el mismo scope.
        public Task<Viaje?> GetEnCursoPorDeviceAsync(string deviceId, CancellationToken ct = default)
            => _db.Viajes
                  .Where(v => v.DeviceId == deviceId && v.Estado == EstadoViaje.EnCurso)
                  .OrderByDescending(v => v.InicioUtc)
                  .FirstOrDefaultAsync(ct);

        public Task<List<Viaje>> ListByVehiculoAsync(
            Guid vehiculoId, DateTime? desdeUtc, DateTime? hastaUtc,
            int skip, int take, CancellationToken ct = default)
            => Filtrar(vehiculoId, desdeUtc, hastaUtc)
                  .AsNoTracking()
                  .OrderByDescending(v => v.InicioUtc)
                  .Skip(skip)
                  .Take(take)
                  .ToListAsync(ct);

        public Task<int> CountByVehiculoAsync(
            Guid vehiculoId, DateTime? desdeUtc, DateTime? hastaUtc, CancellationToken ct = default)
            => Filtrar(vehiculoId, desdeUtc, hastaUtc).CountAsync(ct);

        public Task<List<Viaje>> ListCandidatosCierreAsync(DateTime sinActividadDesdeUtc, CancellationToken ct = default)
            => _db.Viajes
                  .Where(v => v.Estado == EstadoViaje.EnCurso)
                  // Último fix del viaje; si nunca llegó ninguno, cae al inicio
                  // del viaje (así un viaje abierto por error también se cierra).
                  .Where(v => (_db.GpsFixes
                                  .Where(f => f.ViajeId == v.Id)
                                  .Max(f => (DateTime?)f.Utc) ?? v.InicioUtc) < sinActividadDesdeUtc)
                  .ToListAsync(ct);

        public async Task<(int Cantidad, decimal Total)> ResumenTransitosAsync(Guid viajeId, CancellationToken ct = default)
        {
            var r = await _db.Transitos.AsNoTracking()
                .Where(t => t.ViajeId == viajeId)
                .GroupBy(_ => 1)
                .Select(g => new { Cantidad = g.Count(), Total = g.Sum(x => x.PrecioCalculado) })
                .FirstOrDefaultAsync(ct);

            return r is null ? (0, 0m) : (r.Cantidad, r.Total);
        }

        public async Task AddAsync(Viaje viaje, CancellationToken ct = default)
            => await _db.Viajes.AddAsync(viaje, ct);

        private IQueryable<Viaje> Filtrar(Guid vehiculoId, DateTime? desdeUtc, DateTime? hastaUtc)
        {
            var q = _db.Viajes.Where(v => v.VehiculoId == vehiculoId);
            if (desdeUtc is DateTime d) q = q.Where(v => v.InicioUtc >= d);
            if (hastaUtc is DateTime h) q = q.Where(v => v.InicioUtc <= h);
            return q;
        }
    }
}
