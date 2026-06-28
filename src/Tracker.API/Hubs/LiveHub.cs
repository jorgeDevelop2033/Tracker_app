using Microsoft.AspNetCore.SignalR;

namespace Tracker.API.Hubs;

/// <summary>
/// Hub de SALIDA hacia los dashboards (viewers). No recibe GPS del móvil
/// (esa ingesta sigue en Tracker.WebSocket → Kafka → Worker); aquí solo se
/// reemiten posiciones ya persistidas para visualización en vivo.
///
/// El dashboard se une al grupo de un dispositivo con <see cref="Subscribe"/>
/// y recibe el evento "position" cada vez que llega un fix nuevo de ese device.
/// </summary>
public sealed class LiveHub : Hub
{
    /// <summary>Nombre del grupo SignalR para un dispositivo.</summary>
    public static string GroupFor(string deviceId) => $"device:{deviceId}";

    /// <summary>El viewer empieza a seguir a un dispositivo.</summary>
    public Task Subscribe(string deviceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(deviceId));

    /// <summary>El viewer deja de seguir a un dispositivo.</summary>
    public Task Unsubscribe(string deviceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(deviceId));
}
