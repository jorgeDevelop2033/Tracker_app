// Tracker.Infrastructure/Persistence/TrackerDbContext.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence
{
    public class TrackerDbContext : DbContext
    {
        public TrackerDbContext(DbContextOptions<TrackerDbContext> options) : base(options) { }

        // DbSets
        public DbSet<Portico> Porticos => Set<Portico>();
        public DbSet<Transito> Transitos => Set<Transito>();
        public DbSet<TarifaPortico> TarifasPortico => Set<TarifaPortico>();
        public DbSet<GpsFix> GpsFixes => Set<GpsFix>();
         

        public Task<int> SaveChangesAsync(CancellationToken ct) => base.SaveChangesAsync(ct);
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Esquema por defecto
            modelBuilder.HasDefaultSchema("tracker");

            // Aplica todas las configuraciones explícitas (IEntityTypeConfiguration<>)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackerDbContext).Assembly);

            // ---- Convenciones mínimas seguras (por si alguna entidad no tiene config explícita) ----
            ApplyUtcDateTimeConvention(modelBuilder);
            ApplyRowVersionConvention(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private static void ApplyUtcDateTimeConvention(ModelBuilder modelBuilder)
        {
            // Cualquier propiedad DateTime que termine en "Utc" -> datetime2
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var prop in entity.GetProperties().Where(p =>
                         p.ClrType == typeof(DateTime) &&
                         (p.Name.EndsWith("Utc", StringComparison.Ordinal) || p.Name.Equals("Utc", StringComparison.Ordinal))))
                {
                    prop.SetColumnType("datetime2");
                }
            }
        }

        private static void ApplyRowVersionConvention(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entity.ClrType;
                if (clr is null) continue;

                // Si hereda de tu BaseEntity y tiene RowVersion (byte[])
                if (typeof(Tracker.Domain.Common.BaseEntity).IsAssignableFrom(clr))
                {
                    var rv = entity.FindProperty(nameof(Tracker.Domain.Common.BaseEntity.RowVersion));
                    if (rv is null) continue;

                    // Concurrency token
                    rv.IsConcurrencyToken = true;

                    // IMPORTANTE: asignar, no "SetValueGenerated(...)"
                    rv.ValueGenerated = ValueGenerated.OnAddOrUpdate;

                    // Comportamiento de guardado: SQL Server genera el valor
                    rv.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                    rv.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

                    // Tipo de columna para SQL Server
                    rv.SetColumnName("rowversion");
                    rv.SetColumnType("rowversion"); // alias de timestamp en SQL Server
                }
            }
        }
    }
}
