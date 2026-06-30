using Tracker.Contracts.Enums;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;

namespace Tracker.Domain.Tarifas
{
    public interface IBandaHorarioRepository : IRepository<BandaHorario>
    {
        /// <summary>
        /// Resuelve la banda aplicable en un pórtico para un día/hora local.
        /// Devuelve la banda de la ventana que contenga la hora; si no cae en
        /// ninguna ventana cargada, retorna <see cref="Banda.TBFP"/> (fuera de punta).
        /// Si dos ventanas se solapan, gana la de mayor banda (TS &gt; TBP).
        /// </summary>
        Task<Banda> ResolverBandaAsync(
            Guid porticoId, DiaTipo diaTipo, TimeOnly horaLocal, CancellationToken ct = default);

        /// <summary>Todas las ventanas cargadas de un pórtico (admin/seed).</summary>
        Task<IReadOnlyList<BandaHorario>> GetByPorticoAsync(Guid porticoId, CancellationToken ct = default);
    }
}
