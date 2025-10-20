using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracker.Domain.Common;

namespace Tracker.Domain.Entities
{
    public sealed class GpsFix : BaseEntity
    {
        // Origen / dispositivo
        public string DeviceId { get; set; } = default!;

        // Coordenadas (también mantenemos Lat/Lon “planas” para lectura rápida)
        public double Lat { get; set; }
        public double Lon { get; set; }

        // Métricas opcionales (en el proto eran optional)
        public double? SpeedKph { get; set; }
        public double? HeadingDeg { get; set; }
        public double? AccuracyM { get; set; }

        // Tiempos
        public DateTime Utc { get; set; }                 // evento del dispositivo (UTC)
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow; // cuándo lo guardamos

        // Spatial (SQL Server geography, SRID 4326) — NTS usa Point(lon, lat)
        public Point Location { get; set; } = default!;

        // Idempotencia por posición en Kafka
        public string KafkaTopic { get; set; } = default!;
        public int KafkaPartition { get; set; }
        public long KafkaOffset { get; set; }
    }
}
