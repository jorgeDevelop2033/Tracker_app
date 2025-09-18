using Tracker.WebSocket.DTOs;

namespace Tracker.WebSocket.Services
{
    public interface ITrackerService
    {
        Task ProcessCoordinateAsync(CoordinateDto coordinate);
    }

}