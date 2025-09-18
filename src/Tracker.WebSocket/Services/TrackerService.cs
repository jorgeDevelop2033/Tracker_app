using Tracker.WebSocket.DTOs;

namespace Tracker.WebSocket.Services
{
    public class TrackerService : ITrackerService
    {
        private readonly ILogger<TrackerService> _logger;

        public TrackerService(ILogger<TrackerService> logger)
        {
            _logger = logger;
        }

        public Task ProcessCoordinateAsync(CoordinateDto coordinate)
        {
            // 🚀 Etapa inicial: guardar en logs (luego se enviará a colas o DB)
            _logger.LogInformation("📍 Coordenada recibida - Device:{DeviceId} Lat:{Lat} Lon:{Lon} Time:{Time}",
                coordinate.DeviceId,
                coordinate.Latitude,
                coordinate.Longitude,
                coordinate.Timestamp);

            return Task.CompletedTask;
        }
    }
}
