using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.Worker.Application.Dtos
{
    public sealed record GpsEventDto(
    string DeviceId,
    double Lat,
    double Lon,
    double? SpeedKph,
    double? HeadingDeg,
    DateTime Utc,         // Utc kind
    double? AccuracyM
);
}
