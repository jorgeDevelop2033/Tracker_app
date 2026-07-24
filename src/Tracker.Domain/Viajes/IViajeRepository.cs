#nullable enable
using Tracker.Domain.Entities;

namespace Tracker.Domain.Viajes
{
    public interface IViajeRepository
    {
        Task<Viaje?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Viaje abierto del device, si lo hay. Consulta caliente: la corre cada fix.</summary>
        Task<Viaje?> GetEnCursoPorDeviceAsync(string deviceId, CancellationToken ct = default);

        Task<List<Viaje>> ListByVehiculoAsync(
            Guid vehiculoId, DateTime? desdeUtc, DateTime? hastaUtc,
            int skip, int take, CancellationToken ct = default);

        Task<int> CountByVehiculoAsync(
            Guid vehiculoId, DateTime? desdeUtc, DateTime? hastaUtc, CancellationToken ct = default);

        /// <summary>
        /// Viajes abiertos cuyo último fix es anterior al umbral (o que nunca
        /// recibieron uno y arrancaron antes). Son los que el job debe cerrar.
        /// </summary>
        Task<List<Viaje>> ListCandidatosCierreAsync(DateTime sinActividadDesdeUtc, CancellationToken ct = default);

        /// <summary>
        /// Recuento y suma de los tránsitos del viaje, agregados en la BD.
        /// Se usa al cerrar para dejar los totales desnormalizados exactos,
        /// aunque el conteo incremental haya derivado.
        /// </summary>
        Task<(int Cantidad, decimal Total)> ResumenTransitosAsync(Guid viajeId, CancellationToken ct = default);

        Task AddAsync(Viaje viaje, CancellationToken ct = default);
    }
}
