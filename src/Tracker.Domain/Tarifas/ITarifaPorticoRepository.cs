using Tracker.Contracts.Enums;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;

namespace Tracker.Domain.Tarifas
{
    public interface ITarifaPorticoRepository : IRepository<TarifaPortico>
    {
        Task<TarifaPortico?> GetVigenteAsync(Guid porticoId, VehicleCategory categoria, Banda banda, DateTimeOffset atUtc, CancellationToken ct = default);
        Task<IReadOnlyList<TarifaPortico>> GetHistorialAsync(Guid porticoId, VehicleCategory categoria, Banda banda, CancellationToken ct = default);


        /// <summary>
        /// Inserta una nueva vigencia cerrando la anterior si corresponde (snapshot LongitudKm incluido).
        /// </summary>
        Task UpsertVigenciaAsync(TarifaPortico nueva, CancellationToken ct = default);
    }
}
