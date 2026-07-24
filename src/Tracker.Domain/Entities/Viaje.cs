#nullable enable
using NetTopologySuite.Geometries;
using Tracker.Contracts.Enums;
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    /// <summary>
    /// Un trayecto del vehículo, delimitado por el conductor desde la app
    /// (Iniciar/Detener) y cerrado automáticamente por inactividad si la app
    /// muere sin avisar. Agrupa los <see cref="GpsFix"/> del recorrido y los
    /// <see cref="Transito"/> cobrados durante él.
    /// </summary>
    public class Viaje : BaseEntity
    {
        public Guid VehiculoId { get; set; }
        public Vehiculo Vehiculo { get; set; } = default!;

        /// <summary>Device que originó el viaje (el que estaba emitiendo GPS).</summary>
        public string DeviceId { get; set; } = default!;

        public DateTime InicioUtc { get; set; }
        public DateTime? FinUtc { get; set; }

        /// <summary>
        /// Fecha local Chile del inicio. Persistida para poder listar "viajes del
        /// 12 de marzo" con un filtro sobre columna indexada, sin convertir zona
        /// horaria dentro de la query. Ver el mismo criterio en <see cref="Transito.FechaLocal"/>.
        /// </summary>
        public DateOnly FechaLocalInicio { get; set; }

        public EstadoViaje Estado { get; set; } = EstadoViaje.EnCurso;

        public Point? PuntoInicio { get; set; }
        public Point? PuntoFin { get; set; }

        /// <summary>Etiqueta libre del conductor ("A Valparaíso").</summary>
        public string? Nombre { get; set; }
        public string? Nota { get; set; }

        // ---- Totales desnormalizados -------------------------------------
        // Se mantienen al vuelo para que listar viajes no dispare un agregado
        // sobre millones de tránsitos/fixes en cada request.

        public int CantidadTransitos { get; set; }
        public decimal TotalGasto { get; set; }
        public double DistanciaKm { get; set; }
        public int CantidadFixes { get; set; }

        /// <summary>
        /// Recorrido comprimido del viaje (SRID 4326), generado al cerrarlo.
        /// <para>
        /// Es la pieza que permite purgar <c>gps_fix</c> sin perder el historial:
        /// un viaje de 2 h a 1 fix/s son ~7.200 puntos, y simplificado con
        /// Douglas-Peucker a ~10 m quedan unos cientos — visualmente idéntico en
        /// un mapa y ~20x más chico. Los fixes crudos se pueden borrar pasada la
        /// ventana de retención; esta geometría se conserva para siempre.
        /// </para>
        /// </summary>
        public LineString? RutaSimplificada { get; set; }

        public ICollection<Transito> Transitos { get; set; } = new List<Transito>();

        public bool EstaAbierto => Estado == EstadoViaje.EnCurso;
    }
}
