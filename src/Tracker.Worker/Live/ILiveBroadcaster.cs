using Tracker.Application.Dtos;

namespace Tracker.Worker.Live;

/// <summary>
/// Empuja eventos ya persistidos hacia Tracker.API para que los reemita por
/// SignalR a los dashboards. Best-effort: una falla aquí NO debe abortar el
/// consumo de Kafka (la persistencia es la fuente de verdad).
/// </summary>
public interface ILiveBroadcaster
{
    /// <summary>Posición en vivo del vehículo.</summary>
    Task BroadcastAsync(GpsEventDto pos, CancellationToken ct = default);

    /// <summary>Paso por pórtico detectado (con su precio).</summary>
    Task BroadcastTransitoAsync(TransitoDetectadoDto transito, CancellationToken ct = default);
}
