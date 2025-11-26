using Tracker.Domain.Abstractions.Filter;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;
using NetTopologySuite.Geometries;

namespace Tracker.Domain.Transitos
{
    public interface ITransitoRepository : IRepository<Transito>
    {
        Task<PagedResult<Transito>> GetByPorticoAsync(Guid porticoId, DateTimeOffset fromUtc, DateTimeOffset toUtc, Pagination page, CancellationToken ct = default);


        /// <summary>
        /// Búsqueda espacial por buffer de metros alrededor de un punto y franja de tiempo.
        /// </summary>
        Task<PagedResult<Transito>> SearchByAreaAsync(Point center4326, double radiusMeters, DateTimeOffset fromUtc, DateTimeOffset toUtc, Pagination page, CancellationToken ct = default);
    }
}
