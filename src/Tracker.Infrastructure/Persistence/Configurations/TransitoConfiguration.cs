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

            // Enums → int, con default en BD
            b.Property(t => t.Banda)
             .IsRequired()
             .HasConversion<int>()
             .HasDefaultValue(Banda.TBP);

            b.Property(t => t.Categoria)
             .IsRequired()
             .HasConversion<int>()
             .HasDefaultValue(VehicleCategory.C1);

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
        }
    }
}