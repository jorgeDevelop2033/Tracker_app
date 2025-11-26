#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.Application.Services;
using Tracker.Infrastructure.DependencyInjection;
using Tracker.Infrastructure.Persistence;
using Tracker.Infrastructure.Repositories; 
using Tracker.Worker.Application.Services;
using Tracker.Worker.Infrastructure.Services;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine($"[{DateTime.UtcNow:O}] 🔧 Booting Tracker.Worker...");

        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
            o.UseUtcTimestamp = true;
            o.IncludeScopes = false;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // DbContext (SQL Server + NTS)
        builder.Services.AddDbContext<TrackerDbContext>(opt =>
        {
            var cs = builder.Configuration.GetConnectionString("TrackerDb")
                     ?? "Server=localhost,1433;Database=TrackerDb;User Id=sa;Password=09Mayo@84;TrustServerCertificate=True;";
            Console.WriteLine($"🔗 ConnectionString: {cs}");
            opt.UseSqlServer(cs, sql => sql.UseNetTopologySuite());
        });

        builder.Services.AddInfrastructure(builder.Configuration);


        // Repo + Service
        builder.Services.AddScoped<
            Tracker.Domain.Abstractions.IGpsFixRepository,
            Tracker.Infrastructure.Repositories.GpsFixRepository>();

        builder.Services.AddScoped<IGpsIngestService, GpsIngestService>();
        builder.Services.AddScoped<IPorticoDetectionService, PorticoDetectionService>();

        // Hosted Service
        builder.Services.AddHostedService<GpsConsumer>();

        var host = builder.Build();
        Console.WriteLine("✅ Host construido. Iniciando...");

        using (var scope = host.Services.CreateScope())
        {
            // sanity checks de DI (si falla, verás la excepción aquí)
            _ = scope.ServiceProvider.GetRequiredService<Tracker.Domain.Abstractions.IGpsFixRepository>();
            var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            await db.Database.EnsureCreatedAsync(); // o MigrateAsync()
            Console.WriteLine("🗄️  DB ready.");
        }

        await host.RunAsync();
        Console.WriteLine("🏁 Host finalizado.");
    }
}
