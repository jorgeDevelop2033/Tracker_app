#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class AsignacionDispositivoConfiguration : IEntityTypeConfiguration<AsignacionDispositivo>
    {
        public void Configure(EntityTypeBuilder<AsignacionDispositivo> b)
        {
            b.ToTable("AsignacionesDispositivo", schema: "tracker");
            b.HasKey(a => a.Id);

            b.Property(a => a.DeviceId)
             .IsRequired()
             .HasMaxLength(128);

            b.Property(a => a.Nota).HasMaxLength(200);
            b.Property(a => a.DesdeUtc).IsRequired();

            b.HasOne(a => a.Vehiculo)
             .WithMany(v => v.Asignaciones)
             .HasForeignKey(a => a.VehiculoId)
             .OnDelete(DeleteBehavior.Cascade);

            // Resolución "¿de quién era este device en tal instante?".
            b.HasIndex(a => new { a.DeviceId, a.DesdeUtc })
             .HasDatabaseName("ix_asignacion_device_desde");

            b.HasIndex(a => a.VehiculoId)
             .HasDatabaseName("ix_asignacion_vehiculo");

            // Un device no puede estar en dos vehículos a la vez: como máximo una
            // asignación abierta. El índice filtrado lo garantiza en la BD, no
            // solo en el código.
            b.HasIndex(a => a.DeviceId)
             .IsUnique()
             .HasFilter("[HastaUtc] IS NULL")
             .HasDatabaseName("ux_asignacion_device_abierta");
        }
    }
}
