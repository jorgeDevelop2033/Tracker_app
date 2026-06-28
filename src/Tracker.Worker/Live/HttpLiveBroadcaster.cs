using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Tracker.Application.Dtos;

namespace Tracker.Worker.Live;

/// <summary>
/// Implementación que hace POST a Tracker.API (/internal/live). Best-effort:
/// captura cualquier error para no tumbar el pipeline de Kafka.
/// </summary>
public sealed class HttpLiveBroadcaster : ILiveBroadcaster
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpLiveBroadcaster> _log;

    public HttpLiveBroadcaster(HttpClient http, ILogger<HttpLiveBroadcaster> log)
    {
        _http = http;
        _log = log;
    }

    public async Task BroadcastAsync(GpsEventDto pos, CancellationToken ct = default)
    {
        try
        {
            // El contrato de /internal/live (LivePositionDto) es plano; serializamos
            // el GpsEventDto tal cual (mismos campos por nombre).
            using var resp = await _http.PostAsJsonAsync("/internal/live", pos, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Live broadcast respondió {Status} para Device={Device}",
                    (int)resp.StatusCode, pos.DeviceId);
            }
        }
        catch (Exception ex)
        {
            // No relanzamos: el fix ya está persistido; el vivo es secundario.
            _log.LogWarning(ex, "Live broadcast falló para Device={Device}", pos.DeviceId);
        }
    }
}
