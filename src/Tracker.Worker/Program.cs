#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.Application.Services;
using Tracker.Infrastructure.DependencyInjection;
using Tracker.Infrastructure.Persistence;
using Tracker.Infrastructure.Repositories; 
using Tracker.Worker.Application.Services;
using Tracker.Worker.Infrastructure.Services;
using Tracker.Worker.Live;

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

        // Escritura por lotes de gps_fix. El buffer es singleton (vive por encima
        // del scope por mensaje de Kafka) y abre su propio scope al volcar.
        builder.Services.Configure<Tracker.Worker.Ingesta.OpcionesLoteFixes>(
            builder.Configuration.GetSection(Tracker.Worker.Ingesta.OpcionesLoteFixes.SeccionConfig));
        builder.Services.AddSingleton<Tracker.Worker.Ingesta.BufferFixes>();

        // El cierre de viaje pide volcar lo pendiente antes de leer los fixes con
        // los que arma la RutaSimplificada. Replace y no Add: AddInfrastructure ya
        // dejó registrado el no-op, y aquí tiene que ganar el buffer real.
        builder.Services.Replace(ServiceDescriptor.Singleton<Tracker.Domain.Abstractions.IEscriturasPendientes>(
            sp => sp.GetRequiredService<Tracker.Worker.Ingesta.BufferFixes>()));

        // Memoria del fix anterior por device, para detectar pórticos por el
        // trayecto recorrido y no sólo por el punto suelto. Replace: AddInfrastructure
        // ya dejó el no-op registrado y aquí tiene que ganar la cache real.
        builder.Services.Replace(ServiceDescriptor.Singleton<
            Tracker.Application.Services.IUltimaPosicion,
            Tracker.Worker.Ingesta.UltimaPosicionEnMemoria>());

        // Broadcaster en vivo hacia Tracker.API (/internal/live). Best-effort.
        var liveApiBase = builder.Configuration["LiveApi:BaseUrl"] ?? "http://localhost:5000";
        var internalKey = builder.Configuration["InternalApi:Key"] ?? "";
        builder.Services.AddHttpClient<ILiveBroadcaster, HttpLiveBroadcaster>(http =>
        {
            http.BaseAddress = new Uri(liveApiBase);
            http.Timeout = TimeSpan.FromSeconds(3);
            // API key compartida que valida /internal/live en Tracker.API.
            if (!string.IsNullOrEmpty(internalKey))
                http.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });

        // Hosted Services
        builder.Services.AddHostedService<GpsConsumer>();

        // Registrado después de GpsConsumer a propósito: el host detiene los
        // hosted services en orden inverso al registro, así que el consumer deja
        // de encolar antes de que este haga el volcado final del buffer.
        builder.Services.AddHostedService<Tracker.Worker.Ingesta.VolcadoFixesService>();

        // Red de seguridad: cierra viajes que la app dejó abiertos.
        builder.Services.AddHostedService<Tracker.Worker.Workers.CierreViajesService>();

        var host = builder.Build();
        Console.WriteLine("✅ Host construido. Iniciando...");

        using (var scope = host.Services.CreateScope())
        {
            // sanity checks de DI (si falla, verás la excepción aquí)
            _ = scope.ServiceProvider.GetRequiredService<Tracker.Domain.Abstractions.IGpsFixRepository>();
            var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
            await db.Database.EnsureCreatedAsync(); // o MigrateAsync()
            Console.WriteLine("🗄️  DB ready.");

            // Catálogo de pórticos (datos reales OSM). Idempotente: upsert por OsmId.
            var (insertados, actualizados) = await Tracker.Infrastructure.Seed.PorticoSeeder.SeedAsync(db);
            Console.WriteLine($"🛣️  Pórticos seed -> insertados: {insertados}, actualizados: {actualizados}.");
        }

        await host.RunAsync();
        Console.WriteLine("🏁 Host finalizado.");
    }
}
