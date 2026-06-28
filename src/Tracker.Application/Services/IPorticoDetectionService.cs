using Tracker.Application.Dtos;

namespace Tracker.Application.Services
{
    public interface IPorticoDetectionService
    {
        /// <summary>
        /// Detecta si el evento GPS corresponde al paso por un pórtico y, de ser así,
        /// registra el tránsito. Devuelve el tránsito detectado (para notificarlo en
        /// vivo) o <c>null</c> si no hubo cruce válido.
        /// </summary>
        Task<TransitoDetectadoDto?> DetectarYGuardarAsync(GpsEventDto evt, KafkaMetaDto meta, CancellationToken ct);
    }
}
