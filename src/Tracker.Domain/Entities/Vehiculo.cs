#nullable enable
using Tracker.Contracts.Enums;
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    /// <summary>
    /// Vehículo del usuario. Es el sujeto real del cobro: los tránsitos se
    /// atribuyen aquí (no al teléfono), y su <see cref="Categoria"/> es la que
    /// determina qué tarifa aplica en cada pórtico.
    /// </summary>
    public class Vehiculo : BaseEntity
    {
        /// <summary>Patente normalizada (mayúsculas, sin guiones ni espacios). Única.</summary>
        public string Patente { get; set; } = default!;

        /// <summary>Nombre amigable para la UI ("Camioneta roja").</summary>
        public string? Alias { get; set; }

        public VehicleCategory Categoria { get; set; } = VehicleCategory.C1;

        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public int? Anio { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

        public ICollection<AsignacionDispositivo> Asignaciones { get; set; } = new List<AsignacionDispositivo>();
        public ICollection<Viaje> Viajes { get; set; } = new List<Viaje>();

        /// <summary>
        /// Normaliza una patente para comparar/guardar: mayúsculas y sin separadores.
        /// "bb-cc-12" y "BBCC12" son la misma placa.
        /// </summary>
        public static string NormalizarPatente(string patente)
            => new string((patente ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }
}
