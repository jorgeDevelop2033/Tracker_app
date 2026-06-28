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

            e.Property(x => x.OsmId);
            e.Property(x => x.Codigo).HasMaxLength(50).IsRequired();   // P5, P2.1, o fallback OSM<id> / texto del ref OSM
            e.Property(x => x.Autopista).HasMaxLength(80).IsRequired();
            e.Property(x => x.Sentido).HasMaxLength(60).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(200);
            e.Property(x => x.CallesRef).HasMaxLength(200);
            e.Property(x => x.LongitudKm).HasColumnType("decimal(6,3)");

            // Geografía (SRID 4326)
            e.Property(x => x.Ubicacion).HasColumnType("geography"); // Point
            e.Property(x => x.Corredor).HasColumnType("geography");  // LineString

            // Concurrencia
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            // Clave natural del catálogo: id de OpenStreetMap (único cuando no es nulo)
            e.HasIndex(x => x.OsmId)
                .IsUnique()
                .HasFilter("[OsmId] IS NOT NULL");

            // Índice de búsqueda por código/autopista (NO único: en la realidad un mismo
            // código se repite por sentido/calzada, p. ej. pares A→B / B→A).
            e.HasIndex(x => new { x.Autopista, x.Codigo, x.Sentido });
        }
    }
}
