using NetTopologySuite.Geometries;
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    public sealed class TransitoPortico : BaseEntity
    {
        public Guid PorticoId { get; set; }
        public Portico Portico { get; set; } = default!;

        public DateTime TimestampUtc { get; set; }
        public Point GpsPunto { get; set; } = default!; // SRID 4326

        public double? VelocidadKmh { get; set; }
        public double? HeadingGrados { get; set; }
        public double DistanciaMetros { get; set; }   // distancia al pórtico (o al corredor)

        public string? SourceDeviceId { get; set; }   // móvil/camión
        public string? RawId { get; set; }            // id crudo del evento (para idempotencia)
        public string? Via { get; set; }              // Autopista/Concesión cacheada
        public string? Sentido { get; set; }          // cacheado
    }
}
