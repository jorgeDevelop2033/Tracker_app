using NetTopologySuite.Geometries;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;

namespace Tracker.Domain.Porticos
{
    public interface IPorticoRepository : IRepository<Portico>
    {
        Task<Portico?> GetByCodigoAsync(string codigo, CancellationToken ct = default);


        /// <summary>
        /// Porticos a una distancia (metros) de una posición.
        /// </summary>
        Task<IReadOnlyList<Portico>> GetNearAsync(Point position4326, double maxDistanceMeters, int take = 20, CancellationToken ct = default);


        /// <summary>
        /// Intersección con un corredor (línea) – devuelve candidatos ordenados por distancia.
        /// </summary>
        Task<IReadOnlyList<Portico>> IntersectsCorredorAsync(LineString corredor4326, int take = 50, CancellationToken ct = default);
    }
}
