using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Configurations
{
    public class TarifaPorticoConfiguration : IEntityTypeConfiguration<TarifaPortico>
    {
        public void Configure(EntityTypeBuilder<TarifaPortico> e)
        {
            e.ToTable("TarifasPortico");

            // Clave y relación
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Portico)
             .WithMany(p => p.Tarifas)
             .HasForeignKey(x => x.PorticoId)
             .OnDelete(DeleteBehavior.Cascade);

            // Propiedades
            e.Property(x => x.Categoria).IsRequired();
            e.Property(x => x.Banda).HasMaxLength(8).IsRequired();

            e.Property(x => x.ValorFijo).HasColumnType("decimal(8,2)");   // si hay valor directo
            e.Property(x => x.ValorPorKm).HasColumnType("decimal(8,3)");  // si es por km
            e.Property(x => x.LongitudKmSnapshot).HasColumnType("decimal(6,3)");

            e.Property(x => x.VigenteDesde).IsRequired();
            e.Property(x => x.VigenteHasta);

            // Concurrencia
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            // Índices útiles para resolver tarifas vigentes
            e.HasIndex(x => new { x.PorticoId, x.Categoria, x.Banda, x.VigenteDesde, x.VigenteHasta });
            e.HasIndex(x => new { x.PorticoId, x.Banda, x.VigenteDesde });
        }
    }
}
