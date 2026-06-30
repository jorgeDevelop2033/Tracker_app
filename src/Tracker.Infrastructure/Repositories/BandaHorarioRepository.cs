using Microsoft.EntityFrameworkCore;
using Tracker.Contracts.Enums;
using Tracker.Domain.Entities;
using Tracker.Domain.Tarifas;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Repositories
{
    public sealed class BandaHorarioRepository(TrackerDbContext db)
        : EfRepositoryBase<BandaHorario>(db), IBandaHorarioRepository
    {
        public async Task<Banda> ResolverBandaAsync(
            Guid porticoId, DiaTipo diaTipo, TimeOnly horaLocal, CancellationToken ct = default)
        {
            var ventanas = await _db.BandasHorario.AsNoTracking()
                .Where(b => b.PorticoId == porticoId && b.DiaTipo == diaTipo)
                .Select(b => new { b.HoraInicio, b.HoraFin, b.Banda })
                .ToListAsync(ct);

            var banda = Banda.TBFP; // default fuera de toda ventana
            foreach (var v in ventanas)
            {
                if (!Contiene(v.HoraInicio, v.HoraFin, horaLocal)) continue;
                // Si solapan ventanas, gana la de mayor banda (TS=2 > TBP=1 > TBFP=0).
                if (v.Banda > banda) banda = v.Banda;
            }
            return banda;
        }

        public async Task<IReadOnlyList<BandaHorario>> GetByPorticoAsync(Guid porticoId, CancellationToken ct = default)
            => await _db.BandasHorario.AsNoTracking()
                .Where(b => b.PorticoId == porticoId)
                .OrderBy(b => b.DiaTipo).ThenBy(b => b.HoraInicio)
                .ToListAsync(ct);

        /// <summary>
        /// HoraInicio inclusive, HoraFin exclusive. Soporta ventanas que cruzan
        /// medianoche (inicio &gt; fin).
        /// </summary>
        private static bool Contiene(TimeOnly inicio, TimeOnly fin, TimeOnly hora)
            => inicio <= fin
                ? hora >= inicio && hora < fin
                : hora >= inicio || hora < fin;
    }
}
