namespace Tracker.Application.Dtos
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
