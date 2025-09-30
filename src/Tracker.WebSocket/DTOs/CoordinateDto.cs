namespace Tracker.WebSocket.DTOs
{
    public class CoordinateDto
    {
        public string DeviceId { get; set; } = default!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow; 
        public double? SpeedKph { get; set; }
        public double? HeadingDeg { get; set; }
        public double? AccuracyM { get; set; }
    }
}
