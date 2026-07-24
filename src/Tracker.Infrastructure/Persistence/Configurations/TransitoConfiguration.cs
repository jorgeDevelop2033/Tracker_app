// Tracker.Infrastructure/Persistence/Configurations/TransitoConfiguration.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Contracts.Enums;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class TransitoConfiguration : IEntityTypeConfiguration<Transito>
    {
        public void Configure(EntityTypeBuilder<Transito> b)
        {
            b.ToTable("Transitos", schema: "tracker");
            b.HasKey(t => t.Id);

            // FK sin navegación inversa (no necesitas agregar ICollection en Portico)
            b.HasOne(t => t.Portico)
             .WithMany()                          // 👈 SIN nav inversa
             .HasForeignKey(t => t.PorticoId)
             .OnDelete(DeleteBehavior.Restrict);

            b.Property(t => t.Utc).IsRequired();

            // Dispositivo GPS que registró el paso (para totalizar gasto por device).
            b.Property(t => t.DeviceId)
             .HasMaxLength(128);

            // Enums → int. SIN HasDefaultValue: el detector siempre asigna Banda
            // y Categoria explícitamente. Con un default en BD, EF trata el valor
            // CLR 0 (TBFP) como "no asignado" y lo reemplaza por el default,
            // guardando TBP en tránsitos fuera de punta aunque el precio sea TBFP.
            b.Property(t => t.Banda)
             .IsRequired()
             .HasConversion<int>();

            b.Property(t => t.Categoria)
             .IsRequired()
             .HasConversion<int>();

            b.Property(t => t.PrecioCalculado)
             .IsRequired()
             .HasPrecision(18, 4)
             .HasDefaultValue(0m);

            b.Property(t => t.Fuente)
             .IsRequired()
             .HasMaxLength(32)
             .HasDefaultValue("GPS");

            b.Property(t => t.Posicion)
             .HasColumnType("geography"); // SQL Server geography SRID 4326

            // ---- Atribución: vehículo y viaje ------------------------------
            // Ambas FK son opcionales: un tránsito de un device sin asignar, o
            // detectado fuera de un viaje, se registra igual (el cobro existe
            // aunque no sepamos a quién imputarlo todavía).

            b.HasOne(t => t.Vehiculo)
             .WithMany()
             .HasForeignKey(t => t.VehiculoId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.Viaje)
             .WithMany(v => v.Transitos)
             .HasForeignKey(t => t.ViajeId)
             .OnDelete(DeleteBehavior.SetNull);

            // Tarifa aplicada: Restrict para que no se pueda borrar una tarifa
            // que respalda cobros ya emitidos.
            b.HasOne(t => t.TarifaPortico)
             .WithMany()
             .HasForeignKey(t => t.TarifaPorticoId)
             .OnDelete(DeleteBehavior.Restrict);

            // ---- Fecha local y snapshots -----------------------------------
            b.Property(t => t.FechaLocal).IsRequired();
            b.Property(t => t.HoraLocal).IsRequired();

            b.Property(t => t.DiaTipo)
             .IsRequired()
             .HasConversion<int>();

            b.Property(t => t.EstadoConciliacion)
             .IsRequired()
             .HasConversion<int>();

            b.Property(t => t.AutopistaSnapshot).HasMaxLength(120);
            b.Property(t => t.PorticoCodigoSnapshot).HasMaxLength(40);
            b.Property(t => t.SentidoSnapshot).HasMaxLength(80);

            b.HasIndex(t => t.PorticoId);
            b.HasIndex(t => t.Utc);
            b.HasIndex(t => new { t.PorticoId, t.Utc });
            // Agregación de gasto por dispositivo y rango de fechas.
            b.HasIndex(t => new { t.DeviceId, t.Utc });

            // Reportes de conciliación: gasto de un vehículo por día local, y
            // corte por autopista. Cubre el GROUP BY sin ordenar en memoria.
            b.HasIndex(t => new { t.VehiculoId, t.FechaLocal })
             .HasDatabaseName("ix_transito_vehiculo_fechalocal");

            b.HasIndex(t => new { t.VehiculoId, t.AutopistaSnapshot, t.FechaLocal })
             .HasDatabaseName("ix_transito_vehiculo_autopista_fecha");

            b.HasIndex(t => t.ViajeId)
             .HasDatabaseName("ix_transito_viaje");

            // Idempotencia dura. El de-bounce de ±90 s es heurístico y vive en
            // el detector; si Kafka reentrega un lote (el commit manual falla
            // después de procesar) se cobraría dos veces el mismo paso. Este
            // índice hace que la BD rechace el duplicado exacto.
            b.HasIndex(t => new { t.DeviceId, t.PorticoId, t.Utc })
             .IsUnique()
             .HasFilter("[DeviceId] IS NOT NULL")
             .HasDatabaseName("ux_transito_device_portico_utc");
        }
    }
}