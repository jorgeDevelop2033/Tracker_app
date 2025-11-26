using Tracker.Application.Dtos;

namespace Tracker.Application.Services
{
    public interface IPorticoDetectionService
    {
        Task DetectarYGuardarAsync(GpsEventDto evt, KafkaMetaDto meta, CancellationToken ct);
    }
}
