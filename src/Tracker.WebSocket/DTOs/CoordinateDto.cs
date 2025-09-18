namespace Tracker.WebSocket.DTOs
{
    public class CoordinateDto
    {
        public string DeviceId { get; set; } = default!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
