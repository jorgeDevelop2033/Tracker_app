using NetTopologySuite.Geometries;
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    public class Transito : BaseEntity
    {
        public Guid PorticoId { get; set; }
        public Portico Portico { get; set; } = default!;
        public DateTime Utc { get; set; }
        public string Banda { get; set; } = default!;         // TBFP/TBP/TS
        public int Categoria { get; set; }                    // 1,2,3,4
        public decimal PrecioCalculado { get; set; }
        public Point? Posicion { get; set; }
        public double? ExactitudM { get; set; }
        public string Fuente { get; set; } = "GPS";
    }
}
