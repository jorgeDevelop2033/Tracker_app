namespace Tracker.Application.Dtos
{
    /// <summary>
    /// Resultado de una detección de paso por pórtico. Lo devuelve
    /// IPorticoDetectionService cuando se registra un tránsito, para que la
    /// capa superior (Worker) lo notifique al dashboard en vivo.
    /// </summary>
    public sealed record TransitoDetectadoDto(
        string DeviceId,
        System.Guid PorticoId,
        string PorticoCodigo,
        string Autopista,
        decimal Precio,
        double Lat,
        double Lon,
        System.DateTime Utc
    );
}
