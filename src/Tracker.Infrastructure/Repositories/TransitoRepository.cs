using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Abstractions.Filter;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;
using Tracker.Domain.Transitos;
using Tracker.Infrastructure.Persistence;
using NetTopologySuite.Geometries;

namespace Tracker.Infrastructure.Repositories
{
    public sealed class TransitoRepository(TrackerDbContext db) : EfRepositoryBase<Transito>(db), ITransitoRepository
    {
        public async Task<PagedResult<Transito>> GetByPorticoAsync(Guid porticoId, DateTimeOffset fromUtc, DateTimeOffset toUtc, Pagination page, CancellationToken ct = default)
        {
            var query = _db.Transitos.AsNoTracking().Where(t => t.PorticoId == porticoId && t.Utc >= fromUtc && t.Utc <= toUtc);
            var total = await query.CountAsync(ct);
            var items = await query
            .OrderByDescending(t => t.Utc)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(ct);
            return new(items, total, page.PageNumber, page.PageSize);
        }


        public async Task<PagedResult<Transito>> SearchByAreaAsync(Point center4326, double radiusMeters, DateTimeOffset fromUtc, DateTimeOffset toUtc, Pagination page, CancellationToken ct = default)
        {
            var query = _db.Transitos.AsNoTracking()
            .Where(t => t.Utc >= fromUtc && t.Utc <= toUtc && t.Posicion.Distance(center4326) <= radiusMeters);


            var total = await query.CountAsync(ct);
            var items = await query
            .OrderBy(t => t.Posicion.Distance(center4326))
            .ThenByDescending(t => t.Utc)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(ct);


            return new(items, total, page.PageNumber, page.PageSize);
        }
    }
}
