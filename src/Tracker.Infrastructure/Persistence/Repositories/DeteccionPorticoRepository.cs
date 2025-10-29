#nullable enable
using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Repositories;

namespace Tracker.Infrastructure.Persistence.Repositories;

internal sealed class DeteccionPorticoRepository : RepositoryBase<PasoPorPortico>, IDeteccionPorticoRepository
{
    private readonly TrackerDbContext _db;

    public DeteccionPorticoRepository(TrackerDbContext db) : base(db) => _db = db;

    public async Task RegistrarAsync(PasoPorPortico d, CancellationToken ct = default)
    {
        await _db.Set<PasoPorPortico>().AddAsync(d, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistePasoAsync(Guid deviceId, Guid porticoId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default)
        => await _db.Set<PasoPorPortico>()
            .AsNoTracking()
            .AnyAsync(x => x.DeviceId == deviceId && x.PorticoId == porticoId && x.TimestampUtc >= desde && x.TimestampUtc <= hasta, ct);
}
