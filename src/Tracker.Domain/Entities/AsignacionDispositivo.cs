#nullable enable
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    /// <summary>
    /// Vigencia de un dispositivo (teléfono) sobre un vehículo.
    /// <para>
    /// Existe para que el historial no se reescriba: un tránsito de marzo debe
    /// seguir atribuido al vehículo que llevaba ese teléfono en marzo, aunque hoy
    /// el mismo teléfono viaje en otro auto. Por eso la resolución es siempre
    /// "¿de quién era este device <b>en tal instante</b>?" y nunca "¿de quién es hoy?".
    /// </para>
    /// </summary>
    public class AsignacionDispositivo : BaseEntity
    {
        public string DeviceId { get; set; } = default!;

        public Guid VehiculoId { get; set; }
        public Vehiculo Vehiculo { get; set; } = default!;

        public DateTime DesdeUtc { get; set; }

        /// <summary>Null = asignación abierta (el device está hoy en este vehículo).</summary>
        public DateTime? HastaUtc { get; set; }

        public string? Nota { get; set; }

        /// <summary>¿Cubre esta asignación el instante dado?</summary>
        public bool CubreA(DateTime utc)
            => DesdeUtc <= utc && (HastaUtc is null || utc < HastaUtc);
    }
}
