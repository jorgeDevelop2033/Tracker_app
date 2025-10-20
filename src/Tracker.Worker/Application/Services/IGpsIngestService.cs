using Tracker.Worker.Application.Dtos;

namespace Tracker.Worker.Application.Services
{

    public interface IGpsIngestService
    {
        Task IngestAsync(GpsEventDto dto, KafkaMetaDto meta, CancellationToken ct);
    }
}
