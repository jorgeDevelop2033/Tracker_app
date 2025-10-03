using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.Domain.Entities
{
    public class GpsFix
    {
        public long Id { get; set; }
        public string DeviceId { get; set; } = default!;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double SpeedKph { get; set; }
        public double HeadingDeg { get; set; }
        public DateTime Utc { get; set; }
        public double? AccuracyM { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
