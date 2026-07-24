#nullable enable
using Tracker.Contracts.Enums;
using Tracker.Domain.Entities;

namespace Tracker.Application.Services
{
    public interface IViajeService
    {
        /// <summary>
        /// Abre un viaje para el device. Si el vehículo no viene dado, se resuelve
        /// por la asignación vigente. Si ya había un viaje abierto para ese device,
        /// se cierra antes (nunca dos viajes vivos por el mismo teléfono).
        /// </summary>
        Task<Viaje> IniciarAsync(string deviceId, Guid? vehiculoId, string? nombre, DateTime utc, CancellationToken ct = default);

        /// <summary>
        /// Cierra el viaje: fija <c>FinUtc</c>, recalcula totales desde la BD y
        /// genera la ruta simplificada a partir de los fixes.
        /// </summary>
        Task<Viaje?> FinalizarAsync(Guid viajeId, DateTime utc, EstadoViaje estadoFinal, CancellationToken ct = default);

        /// <summary>Viaje abierto del device, o null. Consulta caliente del pipeline.</summary>
        Task<Viaje?> ObtenerEnCursoAsync(string deviceId, CancellationToken ct = default);
    }
}
