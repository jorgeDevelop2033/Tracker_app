using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Configurations
{
    public class TransitoConfiguration : IEntityTypeConfiguration<Transito>
    {
        public void Configure(EntityTypeBuilder<Transito> e)
        {
            e.ToTable("Transitos");

            // Claves y relaciones
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Portico)
             .WithMany()
             .HasForeignKey(x => x.PorticoId)
             .OnDelete(DeleteBehavior.Restrict);

            // Propiedades
            e.Property(x => x.Banda).HasMaxLength(8);
            e.Property(x => x.Categoria);
            e.Property(x => x.PrecioCalculado).HasColumnType("decimal(12,2)");
            e.Property(x => x.Posicion).HasColumnType("geography"); // Point (SRID 4326)
            e.Property(x => x.ExactitudM);
            e.Property(x => x.Fuente).HasMaxLength(20);

            // Concurrencia (rowversion)
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            // Índices
            e.HasIndex(x => x.Utc);
            e.HasIndex(x => new { x.PorticoId, x.Utc });
        }
    }
}
