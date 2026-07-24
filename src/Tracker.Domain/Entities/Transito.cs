#nullable enable
using NetTopologySuite.Geometries;
using Tracker.Domain.Common;
using Tracker.Contracts.Enums; // <- Banda, VehicleCategory, EstadoConciliacion

namespace Tracker.Domain.Entities
{
    public class Transito : BaseEntity
    {
        public Guid PorticoId { get; set; }
        public Portico Portico { get; set; } = default!;

        // Dispositivo (GPS) que registró el paso. Permite totalizar el gasto por device.
        public string? DeviceId { get; set; }

        /// <summary>
        /// Vehículo al que se le cobra, resuelto por la asignación vigente del
        /// device en el instante del paso. Null si el device no estaba asignado.
        /// </summary>
        public Guid? VehiculoId { get; set; }
        public Vehiculo? Vehiculo { get; set; }

        /// <summary>Viaje en curso al momento del paso, si lo había.</summary>
        public Guid? ViajeId { get; set; }
        public Viaje? Viaje { get; set; }

        public DateTime Utc { get; set; }

        /// <summary>
        /// Fecha local Chile (America/Santiago) del tránsito, calculada al
        /// detectarlo y persistida.
        /// <para>
        /// Es deliberado guardarla en vez de derivarla al consultar: agrupar por
        /// <c>Utc</c> mete los tránsitos nocturnos en el día siguiente (Chile es
        /// UTC-4/-3), y convertir zona horaria dentro de la query impide usar
        /// índices y obliga a traer todo a memoria. Además congela el día del
        /// reporte aunque después cambie la config de zona horaria.
        /// </para>
        /// </summary>
        public DateOnly FechaLocal { get; set; }

        /// <summary>Hora local Chile del paso, para el detalle de la cartola.</summary>
        public TimeOnly HoraLocal { get; set; }

        /// <summary>Tipo de día usado para resolver la banda. Se guarda para poder auditarlo.</summary>
        public DiaTipo DiaTipo { get; set; } = DiaTipo.Laboral;

        // ← enums con defaults para evitar NULL
        public Banda Banda { get; set; } = Banda.TBP;
        public VehicleCategory Categoria { get; set; } = VehicleCategory.C1;

        public decimal PrecioCalculado { get; set; } = 0m;

        /// <summary>
        /// Tarifa concreta que produjo <see cref="PrecioCalculado"/>. Sin esto no
        /// se puede explicar por qué un cobro de hace meses dio ese monto, ni
        /// distinguir "tarifa 0" de "no había tarifa cargada".
        /// </summary>
        public Guid? TarifaPorticoId { get; set; }
        public TarifaPortico? TarifaPortico { get; set; }

        // ---- Snapshots del catálogo -------------------------------------
        // Portico.Autopista y Portico.Codigo son editables (el reetiquetado por
        // concesión cambió 135 pórticos de una vez). Si el reporte hace join en
        // vivo, ese día se reescribe la historia y una conciliación ya firmada
        // deja de cuadrar. Congelamos lo que se usa para agrupar y mostrar.

        public string? AutopistaSnapshot { get; set; }
        public string? PorticoCodigoSnapshot { get; set; }
        public string? SentidoSnapshot { get; set; }

        /// <summary>Estado frente al cobro real de la concesionaria (fase C).</summary>
        public EstadoConciliacion EstadoConciliacion { get; set; } = EstadoConciliacion.Pendiente;

        // SRID 4326
        public Point? Posicion { get; set; }

        public double? ExactitudM { get; set; }

        public string Fuente { get; set; } = "GPS";
    }
}
