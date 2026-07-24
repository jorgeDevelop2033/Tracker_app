#nullable enable
using Tracker.Domain.Entities;

namespace Tracker.Domain.Vehiculos
{
    public interface IAsignacionDispositivoRepository
    {
        /// <summary>
        /// Asignación que cubría el instante dado. Es la consulta que mantiene
        /// coherente el historial: responde "de quién era este teléfono
        /// <b>entonces</b>", no "de quién es ahora".
        /// </summary>
        Task<AsignacionDispositivo?> GetVigenteAsync(string deviceId, DateTime utc, CancellationToken ct = default);

        /// <summary>Asignación abierta (sin fecha de término) del device, si existe.</summary>
        Task<AsignacionDispositivo?> GetAbiertaAsync(string deviceId, CancellationToken ct = default);

        Task<List<AsignacionDispositivo>> ListByVehiculoAsync(Guid vehiculoId, CancellationToken ct = default);

        Task AddAsync(AsignacionDispositivo asignacion, CancellationToken ct = default);
    }
}
