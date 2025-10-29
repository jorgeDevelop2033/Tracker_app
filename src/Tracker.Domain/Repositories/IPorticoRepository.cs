#nullable enable
using NetTopologySuite.Geometries;
using Tracker.Domain.Entities;

namespace Tracker.Domain.Repositories;

public interface IPorticoRepository : IReadRepository<Portico>, IRepository<Portico>
{
    Task<Portico?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<Portico>> GetVigentesAsync(CancellationToken ct = default);
    /// <summary>
    /// Devuelve pórticos cercanos a un punto geográfico (SRID 4326) dentro de un radio en metros.
    /// </summary>
    Task<IReadOnlyList<Portico>> GetNearAsync(Point location4326, double radiusMeters, bool soloVigentes = true, CancellationToken ct = default);
}
