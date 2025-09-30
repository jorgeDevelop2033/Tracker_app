using System;

namespace Tracker.Contracts;

/// <summary>
/// Resultado preliminar del geofencing: posición asociada al pórtico más probable.
/// </summary>
public sealed record TransitCandidate(
    string DeviceId,
    Guid PorticoId,
    string PorticoCodigo,   // P5, P2.1, etc.
    string Autopista,
    string Sentido,         // "Oriente - Poniente" | "Poniente - Oriente"
    double DistanceM,       // distancia en metros del GPS al buffer del pórtico
    DateTime Utc
);
