using Microsoft.AspNetCore.SignalR;
using Tracker.Contracts;
using Tracker.WebSocket.DTOs;
using Tracker.WebSocket.Messaging;

namespace Tracker.WebSocket.Hubs
{
    public class TrackerHub : Hub
    {
        private readonly IKafkaPublisher _bus;
        private readonly IConfiguration _cfg;
        private readonly ILogger<TrackerHub> _log;

        public TrackerHub(IKafkaPublisher bus, IConfiguration cfg, ILogger<TrackerHub> log)
        {
            _bus = bus; _cfg = cfg; _log = log;
        }

        public async Task SendCoordinate(CoordinateDto c)
        {
            var deviceId = c.DeviceId ?? Context.ConnectionId;
            _log.LogInformation("📥 SendCoordinate Device={Device} Lat={Lat} Lon={Lon}",
                deviceId, c.Latitude, c.Longitude);

            var ev = new GpsEvent(
                DeviceId: deviceId,
                Lat: c.Latitude,
                Lon: c.Longitude,
                SpeedKph: c.SpeedKph,
                HeadingDeg: c.HeadingDeg,
                Utc: c.Timestamp,
                AccuracyM: c.AccuracyM
            );

            var proto = ev.ToProto();
            var topic = _cfg["Kafka:Topic"] ?? "tracker.gps.events";
            try
            {
                await _bus.PublishAsync(topic, proto.DeviceId, proto);
                _log.LogInformation("✅ Publicado a Kafka topic={Topic} Device={Device}", topic, deviceId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "❌ Falló PublishAsync a Kafka (topic={Topic}, Device={Device})", topic, deviceId);
                throw;
            }
        }
    }
}
