using Microsoft.EntityFrameworkCore;
using Tracker.Contracts.Enums;
using Tracker.Domain.Entities;
using Tracker.Domain.Tarifas;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{
    public sealed class TarifaPorticoRepository(TrackerDbContext db) : EfRepositoryBase<TarifaPortico>(db), ITarifaPorticoRepository
    {
        public Task<TarifaPortico?> GetVigenteAsync(Guid porticoId, VehicleCategory categoria, Banda banda, DateTimeOffset atUtc, CancellationToken ct = default)
        => _db.TarifasPortico.AsNoTracking()
        .Where(t => t.PorticoId == porticoId && t.Categoria == categoria && t.Banda == banda)
        .Where(t => t.VigenteDesde <= atUtc && (t.VigenteHasta == null || t.VigenteHasta >= atUtc))
        .OrderByDescending(t => t.VigenteDesde)
        .FirstOrDefaultAsync(ct);


        public async Task<IReadOnlyList<TarifaPortico>> GetHistorialAsync(Guid porticoId, VehicleCategory categoria, Banda banda, CancellationToken ct = default)
        => await _db.TarifasPortico.AsNoTracking()
        .Where(t => t.PorticoId == porticoId && t.Categoria == categoria && t.Banda == banda)
        .OrderByDescending(t => t.VigenteDesde)
        .ToListAsync(ct);


        public async Task UpsertVigenciaAsync(TarifaPortico nueva, CancellationToken ct = default)
        {
            // Cerrar la tarifa vigente si se solapa
            var vigente = await _db.TarifasPortico
            .Where(t => t.PorticoId == nueva.PorticoId && t.Categoria == nueva.Categoria && t.Banda == nueva.Banda)
            .Where(t => t.VigenteHasta == null)
            .OrderByDescending(t => t.VigenteDesde)
            .FirstOrDefaultAsync(ct);


            if (vigente is not null && nueva.VigenteDesde > vigente.VigenteDesde)
            {
                vigente.VigenteHasta = nueva.VigenteDesde.AddTicks(-1);
                _db.TarifasPortico.Update(vigente);
            }


            await _db.TarifasPortico.AddAsync(nueva, ct);
        }
    }
}
