using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class BandaHorarioConfiguration : IEntityTypeConfiguration<BandaHorario>
    {
        public void Configure(EntityTypeBuilder<BandaHorario> e)
        {
            e.ToTable("BandasHorario");

            e.HasKey(x => x.Id);

            e.HasOne(x => x.Portico)
             .WithMany()
             .HasForeignKey(x => x.PorticoId)
             .OnDelete(DeleteBehavior.Cascade);

            // Enums como string (legibles en BD)
            e.Property(x => x.DiaTipo)
             .HasConversion<string>()
             .HasMaxLength(16)
             .IsRequired();

            e.Property(x => x.Banda)
             .HasConversion<string>()
             .HasMaxLength(8)
             .IsRequired();

            // TimeOnly → time(0)
            e.Property(x => x.HoraInicio).HasColumnType("time(0)").IsRequired();
            e.Property(x => x.HoraFin).HasColumnType("time(0)").IsRequired();

            e.Property(x => x.RowVersion).IsRowVersion();

            // Resolución de banda: pórtico + día tipo, filtrando por hora.
            e.HasIndex(x => new { x.PorticoId, x.DiaTipo })
             .HasDatabaseName("IX_BandaHorario_Portico_Dia");
        }
    }
}
