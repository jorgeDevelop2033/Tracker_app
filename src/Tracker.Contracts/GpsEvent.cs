using System;

namespace Tracker.Contracts;

/// <summary>
/// Evento bruto de posición proveniente del móvil (o dispositivo) vía WebSocket.
/// </summary>
public sealed record GpsEvent(
    string DeviceId,
    double Lat,
    double Lon,
    double? SpeedKph,
    double? HeadingDeg,
    DateTime Utc,       // usar siempre UTC
    double? AccuracyM
);
