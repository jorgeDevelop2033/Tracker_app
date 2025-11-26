// Tracker.Worker/Application/Services/GpsIngestService.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Tracker.Domain.Entities;
 

using Tracker.Domain.Abstractions;
using Tracker.Application.Dtos;

namespace Tracker.Worker.Application.Services
{
    public sealed class GpsIngestService : IGpsIngestService
    {
        private readonly IGpsFixRepository _repo;
        private readonly GeometryFactory _geo;
        private readonly ILogger<GpsIngestService> _log;

        public GpsIngestService(IGpsFixRepository repo, ILogger<GpsIngestService> log)
        {
            _repo = repo;
            _log = log;
            _geo = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326); // geography
        }

        public async Task IngestAsync(GpsEventDto dto, KafkaMetaDto meta, CancellationToken ct)
        {
            // Idempotencia rápida por posición en Kafka
            if (await _repo.ExistsKafkaOffsetAsync(meta.Topic, meta.Partition, meta.Offset, ct))
            {
                _log.LogDebug("⏭️ Skip duplicado por offset: {Topic}[{Partition}]@{Offset}", meta.Topic, meta.Partition, meta.Offset);
                return;
            }

            // Validaciones mínimas y rangos
            if (double.IsNaN(dto.Lat) || double.IsNaN(dto.Lon))
                throw new InvalidOperationException("Lat/Lon inválidos (NaN).");
            if (dto.Lat is < -90 or > 90)
                throw new ArgumentOutOfRangeException(nameof(dto.Lat), "Lat debe estar entre -90 y 90.");
            if (dto.Lon is < -180 or > 180)
                throw new ArgumentOutOfRangeException(nameof(dto.Lon), "Lon debe estar entre -180 y 180.");

            // Crea geografía (lon, lat)
            var point = _geo.CreatePoint(new Coordinate(dto.Lon, dto.Lat));

            var entity = new GpsFix
            {
                Id = Guid.NewGuid(),
                DeviceId = dto.DeviceId,
                Lat = dto.Lat,
                Lon = dto.Lon,
                SpeedKph = dto.SpeedKph,
                HeadingDeg = dto.HeadingDeg,
                AccuracyM = dto.AccuracyM,
                Utc = DateTime.SpecifyKind(dto.Utc, DateTimeKind.Utc),
                CreatedUtc = DateTime.UtcNow,
                Location = point,
                KafkaTopic = meta.Topic,
                KafkaPartition = meta.Partition,
                KafkaOffset = meta.Offset
            };

            try
            {
                await _repo.AddAsync(entity, ct); // maneja DbUpdateException por índice único
            }
            catch (DbUpdateException ex) when (IsUniqueOffsetViolation(ex))
            {
                // carrera entre consumidores del mismo grupo, benigno
                _log.LogWarning("⚠️ Offset duplicado (benigno): {Topic}[{Partition}]@{Offset}",
                    meta.Topic, meta.Partition, meta.Offset);
            }
        }

        private static bool IsUniqueOffsetViolation(DbUpdateException ex) =>
            ex.InnerException?.Message.Contains("ux_gpsfix_kafka_position", StringComparison.OrdinalIgnoreCase) == true;
    }
}
