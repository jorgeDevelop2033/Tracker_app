using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Domain.Entities;
// using Tracker.Domain.Common; // enums VehicleCategory, Banda

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

            // ===== Enums como string (legible) =====
            e.Property(x => x.Categoria)
             .HasConversion<string>()     // guarda "C1","C2","C3","C4"
             .HasMaxLength(8)
             .IsRequired();

            e.Property(x => x.Banda)
             .HasConversion<string>()     // guarda "TBFP","TBP","TS"
             .HasMaxLength(8)
             .IsRequired();

            // Montos y métricas
            e.Property(x => x.ValorFijo).HasColumnType("decimal(12,2)");
            e.Property(x => x.ValorPorKm).HasColumnType("decimal(12,6)");
            e.Property(x => x.LongitudKmSnapshot).HasColumnType("decimal(12,6)");

            // Vigencias
            e.Property(x => x.VigenteDesde).IsRequired();
            // VigenteHasta puede ser null (vigencia abierta)

            // Concurrencia (RowVersion)
            e.Property(x => x.RowVersion).IsRowVersion(); // IsConcurrencyToken implícito

            // Índices para resolver tarifas vigentes
            e.HasIndex(x => new { x.PorticoId, x.Categoria, x.Banda, x.VigenteDesde, x.VigenteHasta })
             .HasDatabaseName("IX_Tarifa_Vigencias");
            e.HasIndex(x => new { x.PorticoId, x.Banda, x.VigenteDesde })
             .HasDatabaseName("IX_Tarifa_Banda_Desde");
        }
    }
}
