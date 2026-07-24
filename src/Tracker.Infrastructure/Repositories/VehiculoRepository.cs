#nullable enable
using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;
using Tracker.Domain.Vehiculos;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{
    public sealed class VehiculoRepository : IVehiculoRepository
    {
        private readonly TrackerDbContext _db;

        public VehiculoRepository(TrackerDbContext db) => _db = db;

        public Task<Vehiculo?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);

        public Task<Vehiculo?> GetByPatenteAsync(string patenteNormalizada, CancellationToken ct = default)
            => _db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Patente == patenteNormalizada, ct);

        public Task<List<Vehiculo>> ListAsync(bool soloActivos, CancellationToken ct = default)
            => _db.Vehiculos.AsNoTracking()
                  .Where(v => !soloActivos || v.Activo)
                  .OrderBy(v => v.Patente)
                  .ToListAsync(ct);

        public async Task AddAsync(Vehiculo vehiculo, CancellationToken ct = default)
            => await _db.Vehiculos.AddAsync(vehiculo, ct);
    }
}
