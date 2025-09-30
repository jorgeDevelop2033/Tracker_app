using NetTopologySuite.Geometries;
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    public class Portico : BaseEntity
    {
        public string Codigo { get; set; } = default!;        // P5, P2.1, etc.
        public string Autopista { get; set; } = default!;
        public string Sentido { get; set; } = default!;       // "Oriente - Poniente", etc.
        public string Descripcion { get; set; } = default!;
        public string? CallesRef { get; set; }
        public decimal? LongitudKm { get; set; }

        // Tipos espaciales (SRID 4326)
        public Point? Ubicacion { get; set; }                 // punto representativo
        public LineString? Corredor { get; set; }             // tramo (A-B) si aplica

        public bool Vigente { get; set; } = true;

        public ICollection<TarifaPortico> Tarifas { get; set; } = new List<TarifaPortico>();
    }
}
