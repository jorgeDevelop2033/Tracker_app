using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Persistence
{
    public class TrackerDbContext : DbContext
    {
        public TrackerDbContext(DbContextOptions<TrackerDbContext> options) : base(options) { }

        public DbSet<Portico> Porticos => Set<Portico>();
        public DbSet<Transito> Transitos => Set<Transito>();
        public DbSet<TarifaPortico> TarifasPortico => Set<TarifaPortico>();
        public DbSet<GpsFix> GpsFixes => Set<GpsFix>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackerDbContext).Assembly);
    }
}
