// Tracker.Worker/Application/Services/GpsIngestService.cs
#nullable enable
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Tracker.Domain.Entities;

using Tracker.Application.Dtos;
using Tracker.Domain.Viajes;
using Tracker.Worker.Ingesta;

namespace Tracker.Worker.Application.Services
{
    public sealed class GpsIngestService : IGpsIngestService
    {
        private readonly BufferFixes _buffer;
        private readonly IViajeRepository _viajes;
        private readonly GeometryFactory _geo;
        private readonly ILogger<GpsIngestService> _log;

        public GpsIngestService(BufferFixes buffer, IViajeRepository viajes, ILogger<GpsIngestService> log)
        {
            _buffer = buffer;
            _viajes = viajes;
            _log = log;
            _geo = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326); // geography
        }

        public async Task IngestAsync(GpsEventDto dto, KafkaMetaDto meta, CancellationToken ct)
        {
            // Nota: ya no se consulta la BD para descartar offsets repetidos. Esa
            // comprobación costaba un SELECT por mensaje — el mismo round-trip que
            // el lote busca evitar — y sólo servía para ahorrar una inserción en el
            // caso raro de una reentrega. El índice único ux_gpsfix_kafka_position
            // sigue siendo la garantía real de idempotencia, y el repositorio ya
            // trata su violación como benigna.

            // Validaciones mínimas y rangos
            if (double.IsNaN(dto.Lat) || double.IsNaN(dto.Lon))
                throw new InvalidOperationException("Lat/Lon inválidos (NaN).");
            if (dto.Lat is < -90 or > 90)
                throw new ArgumentOutOfRangeException(nameof(dto.Lat), "Lat debe estar entre -90 y 90.");
            if (dto.Lon is < -180 or > 180)
                throw new ArgumentOutOfRangeException(nameof(dto.Lon), "Lon debe estar entre -180 y 180.");

            // Crea geografía (lon, lat)
            var point = _geo.CreatePoint(new Coordinate(dto.Lon, dto.Lat));

            // Si hay un viaje abierto para este device, el fix queda colgado de él.
            // Eso hace que la ruta del viaje sea un filtro por columna indexada y,
            // más adelante, permite purgar los fixes de viajes ya consolidados.
            var viaje = await _viajes.GetEnCursoPorDeviceAsync(dto.DeviceId, ct);

            var entity = new GpsFix
            {
                Id = Guid.NewGuid(),
                DeviceId = dto.DeviceId,
                ViajeId = viaje?.Id,
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

            // Se difiere la escritura: el buffer la agrupa con otras. La detección
            // de pórtico y el broadcast que vienen después en GpsConsumer no
            // dependen de que la fila ya esté en la BD.
            await _buffer.EncolarAsync(entity, ct);
        }
    }
}
