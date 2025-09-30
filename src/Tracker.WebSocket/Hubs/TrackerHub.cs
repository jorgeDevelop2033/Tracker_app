using Microsoft.AspNetCore.SignalR;
using Tracker.Contracts;
using Tracker.WebSocket.DTOs;
using Tracker.WebSocket.Services;

namespace Tracker.WebSocket.Hubs
{
    public class TrackerHub : Hub
    {
        private readonly ITrackerService _trackerService;

        public TrackerHub(ITrackerService trackerService)
        {
            _trackerService = trackerService;
        }

        public async Task SendCoordinate(CoordinateDto coordinate)
        {
            await _trackerService.ProcessCoordinateAsync(coordinate);

            var ev = new GpsEvent(
           DeviceId: coordinate.DeviceId ?? Context.ConnectionId,
           Lat: coordinate.Latitude, Lon: coordinate.Longitude,
           SpeedKph: coordinate.SpeedKph, HeadingDeg: coordinate.HeadingDeg,
           Utc: coordinate.Timestamp,
           AccuracyM: coordinate.AccuracyM
        );
            //await _bus.Publish(ev); // <-- a la cola

            // Opcional: reenviar en tiempo real a otros clientes conectados
           // await Clients.Others.SendAsync("ReceiveCoordinate", coordinate);
        }
    }
}
