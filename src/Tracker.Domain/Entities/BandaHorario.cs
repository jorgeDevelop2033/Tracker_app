using Tracker.Contracts.Enums;
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    /// <summary>
    /// Ventana horaria que define qué <see cref="Banda"/> aplica en un pórtico
    /// según el tipo de día y la hora local (Chile). Solo se cargan las ventanas
    /// de Punta (TBP) y Saturación (TS); fuera de toda ventana aplica TBFP.
    /// </summary>
    public class BandaHorario : BaseEntity
    {
        public Guid PorticoId { get; set; }
        public Portico Portico { get; set; } = default!;

        public DiaTipo DiaTipo { get; set; }

        /// <summary>Hora local de inicio (inclusive) de la ventana.</summary>
        public TimeOnly HoraInicio { get; set; }

        /// <summary>Hora local de fin (exclusive) de la ventana.</summary>
        public TimeOnly HoraFin { get; set; }

        public Banda Banda { get; set; }
    }
}
