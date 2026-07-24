#nullable enable
using Tracker.Domain.Entities;

namespace Tracker.Domain.Vehiculos
{
    public interface IVehiculoRepository
    {
        Task<Vehiculo?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Busca por patente ya normalizada (ver <see cref="Vehiculo.NormalizarPatente"/>).</summary>
        Task<Vehiculo?> GetByPatenteAsync(string patenteNormalizada, CancellationToken ct = default);

        Task<List<Vehiculo>> ListAsync(bool soloActivos, CancellationToken ct = default);

        Task AddAsync(Vehiculo vehiculo, CancellationToken ct = default);
    }
}
