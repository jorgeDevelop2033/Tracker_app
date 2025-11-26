using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence.Configurations
{
    public sealed class TransitoPorticoConfiguration : IEntityTypeConfiguration<TransitoPortico>
    {
        public void Configure(EntityTypeBuilder<TransitoPortico> b)
        {
            b.ToTable("TransitosPortico");
            b.HasKey(x => x.Id);

            b.Property(x => x.GpsPunto).HasColumnType("geography"); // SRID 4326

            b.HasIndex(x => x.TimestampUtc);
            b.HasIndex(x => new { x.PorticoId, x.TimestampUtc });

            // Idempotencia básica si tienes RawId del stream
            b.HasIndex(x => x.RawId).IsUnique(false);
        }
    }
}
