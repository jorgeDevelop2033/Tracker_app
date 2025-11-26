#nullable enable
using NetTopologySuite.Geometries;
using Tracker.Domain.Common;
using Tracker.Contracts.Enums; // <- Banda, VehicleCategory

namespace Tracker.Domain.Entities
{
    public class Transito : BaseEntity
    {
        public Guid PorticoId { get; set; }
        public Portico Portico { get; set; } = default!;

        public DateTime Utc { get; set; }

        // ← enums con defaults para evitar NULL
        public Banda Banda { get; set; } = Banda.TBP;
        public VehicleCategory Categoria { get; set; } = VehicleCategory.C1;

        public decimal PrecioCalculado { get; set; } = 0m;

        // SRID 4326
        public Point? Posicion { get; set; }

        public double? ExactitudM { get; set; }

        public string Fuente { get; set; } = "GPS";
    }
}
