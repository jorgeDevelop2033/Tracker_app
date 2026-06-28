using Tracker.Application.Dtos;

namespace Tracker.Worker.Live;

/// <summary>
/// Empuja una posición ya persistida hacia Tracker.API para que la reemita
/// por SignalR a los dashboards. Best-effort: una falla aquí NO debe abortar
/// el consumo de Kafka (la persistencia es la fuente de verdad).
/// </summary>
public interface ILiveBroadcaster
{
    Task BroadcastAsync(GpsEventDto pos, CancellationToken ct = default);
}
