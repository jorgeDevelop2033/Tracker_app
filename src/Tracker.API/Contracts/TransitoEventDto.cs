namespace Tracker.API.Contracts;

/// <summary>
/// Tránsito (paso por pórtico) que la API reemite al dashboard por SignalR
/// con el evento "transito".
/// </summary>
public sealed record TransitoEventDto(
    string DeviceId,
    System.Guid PorticoId,
    string PorticoCodigo,
    string Autopista,
    decimal Precio,
    double Lat,
    double Lon,
    System.DateTime Utc);
