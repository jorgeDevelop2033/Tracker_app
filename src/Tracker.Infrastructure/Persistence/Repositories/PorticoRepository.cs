#nullable enable
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tracker.Domain.Entities;
using Tracker.Domain.Repositories;

namespace Tracker.Infrastructure.Persistence.Repositories;

internal sealed class PorticoRepository : RepositoryBase<Portico>, IPorticoRepository
{
    private readonly TrackerDbContext _db;

    public PorticoRepository(TrackerDbContext db) : base(db) => _db = db;

    public async Task<Portico?> GetByCodigoAsync(string codigo, CancellationToken ct = default) =>
        await _db.Porticos.AsNoTracking().FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

    public async Task<IReadOnlyList<Portico>> GetVigentesAsync(CancellationToken ct = default) =>
        await _db.Porticos.AsNoTracking().Where(p => p.Vigente).ToListAsync(ct);

    /// <summary>
    /// Consulta espacial en SRID 4326. Usa ST_DWithin/Distance según proveedor.
    /// Para SQL Server (geography) funca con Distance en metros si SRID=4326 y geog.
    /// Para PostgreSQL+PostGIS requiere UseNetTopologySuite y traducción ST_DWithin.
    /// </summary>
    public async Task<IReadOnlyList<Portico>> GetNearAsync(Point location4326, double radiusMeters, bool soloVigentes = true, CancellationToken ct = default)
    {
        var q = _db.Porticos.AsNoTracking().Where(p => p.Ubicacion != null);
        if (soloVigentes) q = q.Where(p => p.Vigente);

        // Nota: EF Core con NTS traduce .Distance para geography/geometry según provider.
        // Si usas SQL Server tipo geography: Distance devuelve metros.
        // Si usas geometry: considera transformar/usar ST_DWithin adecuadamente.
        return await q
            .Where(p => p.Ubicacion!.Distance(location4326) <= radiusMeters)
            .OrderBy(p => p.Ubicacion!.Distance(location4326))
            .ToListAsync(ct);
    }
}
