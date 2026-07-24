#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class VehiculoConfiguration : IEntityTypeConfiguration<Vehiculo>
    {
        public void Configure(EntityTypeBuilder<Vehiculo> b)
        {
            b.ToTable("Vehiculos", schema: "tracker");
            b.HasKey(v => v.Id);

            b.Property(v => v.Patente)
             .IsRequired()
             .HasMaxLength(16);

            b.Property(v => v.Alias).HasMaxLength(80);
            b.Property(v => v.Marca).HasMaxLength(60);
            b.Property(v => v.Modelo).HasMaxLength(60);

            b.Property(v => v.Categoria)
             .IsRequired()
             .HasConversion<int>();

            b.Property(v => v.Activo).IsRequired().HasDefaultValue(true);
            b.Property(v => v.CreadoUtc).IsRequired();

            // La patente es la clave natural: evita duplicar el mismo auto.
            b.HasIndex(v => v.Patente)
             .IsUnique()
             .HasDatabaseName("ux_vehiculo_patente");
        }
    }
}
