using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tracker.Domain.Entities;
using Tracker.Domain.Porticos;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{

    public sealed class PorticoRepository(TrackerDbContext db)
        : EfRepositoryBase<Portico>(db), IPorticoRepository
    {
        public Task<Portico?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
            => _db.Porticos.AsNoTracking().FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

        public async Task<IReadOnlyList<Portico>> GetNearAsync(
            Point position4326, double maxDistanceMeters, int take = 20, CancellationToken ct = default)
        {
            // Distance en SQL Server geography = metros
            return await _db.Porticos
                .AsNoTracking()
                .Where(p => p.Ubicacion != null && p.Ubicacion.Distance(position4326) <= maxDistanceMeters)
                .OrderBy(p => p.Ubicacion!.Distance(position4326))
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Portico>> GetNearLineAsync(
            LineString tramo4326, double maxDistanceMeters, int take = 20, CancellationToken ct = default)
        {
            // Igual que GetNearAsync pero midiendo contra el segmento completo:
            // en geography, Distance(point, linestring) es la distancia mínima en
            // metros del pórtico a cualquier punto del tramo.
            return await _db.Porticos
                .AsNoTracking()
                .Where(p => p.Ubicacion != null && p.Ubicacion.Distance(tramo4326) <= maxDistanceMeters)
                .OrderBy(p => p.Ubicacion!.Distance(tramo4326))
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Portico>> IntersectsCorredorAsync(
            LineString corredor4326, int take = 50, CancellationToken ct = default)
        {
            return await _db.Porticos
                .AsNoTracking()
                .Where(p => p.Corredor != null && p.Corredor.Intersects(corredor4326))
                .OrderBy(p => p.Corredor!.Distance(corredor4326))
                .Take(take)
                .ToListAsync(ct);
        }
    }
}
