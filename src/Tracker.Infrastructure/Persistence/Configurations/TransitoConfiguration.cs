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

            b.HasIndex(t => t.PorticoId);
            b.HasIndex(t => t.Utc);
            b.HasIndex(t => new { t.PorticoId, t.Utc });
            // Agregación de gasto por dispositivo y rango de fechas.
            b.HasIndex(t => new { t.DeviceId, t.Utc });
        }
    }
}