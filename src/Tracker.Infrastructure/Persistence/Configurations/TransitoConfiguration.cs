// Tracker.Infrastructure/Persistence/Configurations/TransitoConfiguration.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class TransitoConfiguration : IEntityTypeConfiguration<Transito>
    {
        public void Configure(EntityTypeBuilder<Transito> b)
        {
            // 👇 Empata con lo que dicen tus migraciones: (Transitos, tracker)
            b.ToTable("Transitos", schema: "tracker");

            b.HasKey(x => x.Id);

            // RowVersion si no lo manejas por convención
            // b.Property(x => x.RowVersion).IsRowVersion().HasColumnName("rowversion").HasColumnType("rowversion");

            b.Property(x => x.PorticoId).HasColumnName("PorticoId");
            b.Property(x => x.Utc).HasColumnType("datetime2").HasColumnName("Utc");

            b.Property(x => x.Banda).HasMaxLength(16).HasColumnName("Banda");
            b.Property(x => x.Categoria).HasColumnName("Categoria");

            b.Property(x => x.PrecioCalculado).HasColumnType("decimal(18,4)").HasColumnName("PrecioCalculado");

            // geography (SQL Server). NetTopologySuite usa Point(lon, lat)
            b.Property(x => x.Posicion).HasColumnType("geography").HasColumnName("Posicion");

            b.Property(x => x.ExactitudM).HasColumnName("ExactitudM");
            b.Property(x => x.Fuente).HasMaxLength(32).HasColumnName("Fuente");

            b.HasOne(x => x.Portico)
             .WithMany() // ajusta si Portico tiene ICollection<Transito>
             .HasForeignKey(x => x.PorticoId)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_Transitos_Portico");

            b.HasIndex(x => new { x.PorticoId, x.Utc }).HasDatabaseName("IX_Transitos_Portico_Utc");
            b.HasIndex(x => x.Utc).HasDatabaseName("IX_Transitos_Utc");
        }
    }
}
