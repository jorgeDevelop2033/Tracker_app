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

        public TrackerHub(IKafkaPublisher bus, IConfiguration cfg)
        {
            _bus = bus; _cfg = cfg;
        }

        public async Task SendCoordinate(CoordinateDto c)
        {
            var ev = new GpsEvent(
                DeviceId: c.DeviceId ?? Context.ConnectionId,
                Lat: c.Latitude,
                Lon: c.Longitude,
                SpeedKph: c.SpeedKph,
                HeadingDeg: c.HeadingDeg,
                Utc: c.Timestamp,
                AccuracyM: c.AccuracyM
            );

            var proto = ev.ToProto();
            var topic = _cfg["Kafka:Topic"] ?? "tracker.gps.events";
            await _bus.PublishAsync(topic, proto.DeviceId, proto);
        }
    }
}
