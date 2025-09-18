using Microsoft.AspNetCore.SignalR;
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

            // Opcional: reenviar en tiempo real a otros clientes conectados
            await Clients.Others.SendAsync("ReceiveCoordinate", coordinate);
        }
    }
}
