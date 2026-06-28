namespace Tracker.API.Contracts;

/// <summary>
/// Posición que la API expone al frontend (REST y SignalR en vivo).
/// Contrato plano e independiente de las entidades de dominio / NTS.
/// </summary>
public sealed record LivePositionDto(
    string DeviceId,
    double Lat,
    double Lon,
    double? SpeedKph,
    double? HeadingDeg,
    double? AccuracyM,
    DateTime Utc);
