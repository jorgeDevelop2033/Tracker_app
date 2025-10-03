using Google.Protobuf.WellKnownTypes;
using Tracker.Contracts;
using Tracker;

namespace Tracker.WebSocket.DTOs
{
    public static class GpsMapper
    {
        public static GpsEventV2 ToProto(this GpsEvent ev)
        {
            // Asegurar UTC (Timestamp lo requiere en UTC)
            var utc = ev.Utc.Kind == DateTimeKind.Utc ? ev.Utc : ev.Utc.ToUniversalTime();

            var proto = new GpsEventV2
            {
                DeviceId = ev.DeviceId,
                Lat = ev.Lat,
                Lon = ev.Lon,
                Utc = Timestamp.FromDateTime(utc)
            };

            // Solo seteamos si hay valor -> presencia en Protobuf
            if (ev.SpeedKph.HasValue) proto.SpeedKph = ev.SpeedKph.Value;
            if (ev.HeadingDeg.HasValue) proto.HeadingDeg = ev.HeadingDeg.Value;
            if (ev.AccuracyM.HasValue) proto.AccuracyM = ev.AccuracyM.Value;

            return proto;
        }
    }
}
