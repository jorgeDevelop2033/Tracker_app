// Tracker.Infrastructure/Persistence/DesignTimeDbContextFactory.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Tracker.Infrastructure.Persistence
{
    public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TrackerDbContext>
    {
        public TrackerDbContext CreateDbContext(string[] args)
        {
            // Base path para que encuentre appsettings.* cuando se ejecuta "dotnet ef ..."
            var basePath = Directory.GetCurrentDirectory(); // o AppContext.BaseDirectory

            var cfg = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Permite override por env var: ConnectionStrings__TrackerDb
            var cs = cfg.GetConnectionString("TrackerDb")
                     ?? Environment.GetEnvironmentVariable("ConnectionStrings__TrackerDb")
                     ?? "Server=localhost,1433;Database=TrackerDb;User Id=sa;Password=09mayo@84;TrustServerCertificate=True;";

            var options = new DbContextOptionsBuilder<TrackerDbContext>()
                .UseSqlServer(cs, sql => sql.UseNetTopologySuite())
                .Options;

            return new TrackerDbContext(options);
        }
    }
}
