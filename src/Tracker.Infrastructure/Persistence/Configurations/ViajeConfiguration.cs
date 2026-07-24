#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class ViajeConfiguration : IEntityTypeConfiguration<Viaje>
    {
        public void Configure(EntityTypeBuilder<Viaje> b)
        {
            b.ToTable("Viajes", schema: "tracker");
            b.HasKey(v => v.Id);

            b.HasOne(v => v.Vehiculo)
             .WithMany(x => x.Viajes)
             .HasForeignKey(v => v.VehiculoId)
             .OnDelete(DeleteBehavior.Restrict);

            b.Property(v => v.DeviceId)
             .IsRequired()
             .HasMaxLength(128);

            b.Property(v => v.InicioUtc).IsRequired();

            b.Property(v => v.Estado)
             .IsRequired()
             .HasConversion<int>();

            b.Property(v => v.FechaLocalInicio).IsRequired();

            b.Property(v => v.Nombre).HasMaxLength(120);
            b.Property(v => v.Nota).HasMaxLength(500);

            b.Property(v => v.TotalGasto)
             .IsRequired()
             .HasPrecision(18, 4)
             .HasDefaultValue(0m);

            b.Property(v => v.PuntoInicio).HasColumnType("geography");
            b.Property(v => v.PuntoFin).HasColumnType("geography");
            b.Property(v => v.RutaSimplificada).HasColumnType("geography");

            // Listado principal: viajes de un vehículo por fecha, más reciente primero.
            b.HasIndex(v => new { v.VehiculoId, v.InicioUtc })
             .HasDatabaseName("ix_viaje_vehiculo_inicio");

            b.HasIndex(v => new { v.VehiculoId, v.FechaLocalInicio })
             .HasDatabaseName("ix_viaje_vehiculo_fechalocal");

            // El job de cierre por inactividad y la resolución del viaje en curso
            // buscan por device + estado; filtrado para que el índice sea diminuto
            // (los viajes abiertos son un puñado frente a todo el historial).
            b.HasIndex(v => new { v.DeviceId, v.Estado })
             .HasFilter("[Estado] = 0")
             .HasDatabaseName("ix_viaje_abierto_device");
        }
    }
}
