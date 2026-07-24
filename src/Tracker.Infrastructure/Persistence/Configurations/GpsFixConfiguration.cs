using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class GpsFixConfiguration : IEntityTypeConfiguration<GpsFix>
    {
        public void Configure(EntityTypeBuilder<GpsFix> entity)
        {
            // === Tabla y esquema (ajusta si no usas "tracker") ===
            entity.ToTable("gps_fix", schema: "tracker");

            // === PK ===
            entity.HasKey(e => e.Id);

            // === Concurrency token (rowversion/timestamp) ===
            entity.Property(e => e.RowVersion)
                  .IsRowVersion()
                  .IsConcurrencyToken()
                  .HasColumnName("rowversion");

            // === Columnas básicas ===
            entity.Property(e => e.DeviceId)
                  .HasMaxLength(128)
                  .IsRequired()
                  .HasColumnName("device_id");

            // Viaje al que pertenece el fix (null si no había viaje en curso).
            // Sin FK declarada a propósito: gps_fix es una tabla de altísimo
            // volumen y sujeta a purga por lotes; una FK obligaría a validar
            // cada inserción y bloquearía el borrado masivo de fixes antiguos.
            entity.Property(e => e.ViajeId).HasColumnName("viaje_id");

            entity.Property(e => e.Lat).HasColumnName("lat");
            entity.Property(e => e.Lon).HasColumnName("lon");
            entity.Property(e => e.SpeedKph).HasColumnName("speed_kph");
            entity.Property(e => e.HeadingDeg).HasColumnName("heading_deg");
            entity.Property(e => e.AccuracyM).HasColumnName("accuracy_m");

            entity.Property(e => e.Utc)
                  .HasColumnType("datetime2")
                  .HasColumnName("utc");

            entity.Property(e => e.CreatedUtc)
                  .HasColumnType("datetime2")
                  .HasColumnName("created_utc");

            // === Spatial (SQL Server geography) ===
            entity.Property(e => e.Location)
                  .HasColumnType("geography")
                  .HasColumnName("location");

            // === Kafka meta (idempotencia) ===
            entity.Property(e => e.KafkaTopic)
                  .HasMaxLength(200)
                  .IsRequired()
                  .HasColumnName("kafka_topic");

            entity.Property(e => e.KafkaPartition)
                  .HasColumnName("kafka_partition");

            entity.Property(e => e.KafkaOffset)
                  .HasColumnName("kafka_offset");

            // Único por posición en la cola
            entity.HasIndex(e => new { e.KafkaTopic, e.KafkaPartition, e.KafkaOffset })
                  .IsUnique()
                  .HasDatabaseName("ux_gpsfix_kafka_position");

            // Búsquedas frecuentes
            entity.HasIndex(e => new { e.DeviceId, e.Utc })
                  .HasDatabaseName("ix_gpsfix_device_utc");

            // Ruta de un viaje: WHERE viaje_id = x ORDER BY utc. Filtrado para no
            // indexar los fixes sueltos (sin viaje), que son mayoría al principio.
            entity.HasIndex(e => new { e.ViajeId, e.Utc })
                  .HasFilter("[viaje_id] IS NOT NULL")
                  .HasDatabaseName("ix_gpsfix_viaje_utc");
        }
    }
}
