using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Configurations
{
    public class PorticoConfiguration : IEntityTypeConfiguration<Portico>
    {
        public void Configure(EntityTypeBuilder<Portico> e)
        {
            e.ToTable("Porticos");

            e.HasKey(x => x.Id);

            e.Property(x => x.Codigo).HasMaxLength(10).IsRequired();   // P5, P2.1, etc.
            e.Property(x => x.Autopista).HasMaxLength(80).IsRequired();
            e.Property(x => x.Sentido).HasMaxLength(40).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(200);
            e.Property(x => x.CallesRef).HasMaxLength(200);
            e.Property(x => x.LongitudKm).HasColumnType("decimal(6,3)");

            // Geografía (SRID 4326)
            e.Property(x => x.Ubicacion).HasColumnType("geography"); // Point
            e.Property(x => x.Corredor).HasColumnType("geography");  // LineString

            // Concurrencia
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            // Unicidad lógica del catálogo
            e.HasIndex(x => new { x.Autopista, x.Codigo, x.Sentido }).IsUnique();
        }
    }
}
