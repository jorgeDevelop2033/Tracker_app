// Program.cs (API)
using Microsoft.EntityFrameworkCore;
using Tracker.Infrastructure.DependencyInjection; // AddInfrastructure(...)
using Tracker.Infrastructure.Persistence;         // TrackerDbContext


var builder = WebApplication.CreateBuilder(args);

// ===== OpenAPI (.NET 9 minimal) =====
builder.Services.AddOpenApi();

// ===== Infra (EF Core + SQL Server + NTS) =====
// Si tus migraciones viven en Tracker.Infrastructure, NO necesitas nada más.
builder.Services.AddInfrastructure(builder.Configuration);

// Si, en cambio, las migraciones viven en Tracker.Api, usa la sobrecarga (si la expusiste)
// o re-registra explícitamente el DbContext con MigrationsAssembly("Tracker.Api").
builder.Services.AddDbContext<TrackerDbContext>(opt =>
 {
     var cs = builder.Configuration.GetConnectionString("TrackerDb");
     opt.UseSqlServer(cs, sql =>
     {
         sql.UseNetTopologySuite();
         sql.MigrationsAssembly("Tracker.Api");
     });
 });

var app = builder.Build();

// ===== OpenAPI =====
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ===== HTTPS (si usas Kestrel con certificados locales) =====
app.UseHttpsRedirection();

// ===== Endpoints =====
app.MapGet("/health", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }))
   .WithName("Health");

// Mini endpoint para ver pórticos desde la BD
app.MapGet("/api/porticos", async (TrackerDbContext db) =>
{
    var data = await db.Porticos
        .AsNoTracking()
        .OrderBy(p => p.Autopista).ThenBy(p => p.Codigo)
        .Select(p => new
        {
            p.Id,
            p.Autopista,
            p.Codigo,
            p.Sentido,
            p.Descripcion,
            p.LongitudKm
            // Si tienes geography:
            // Lat = p.Ubicacion != null ? p.Ubicacion.Latitude : (double?)null,
            // Lon = p.Ubicacion != null ? p.Ubicacion.Longitude : (double?)null
        })
        .ToListAsync();

    return Results.Ok(data);
}).WithName("GetPorticos");

// ===== Auto-migración al arranque (recomendado si usas migraciones) =====
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                     .CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        await db.Database.MigrateAsync(); // aplica migraciones pendientes
        logger.LogInformation("✅ Migraciones aplicadas correctamente.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error aplicando migraciones.");
        // Decide si quieres relanzar para que el orquestador reinicie el contenedor:
        // throw;
    }
}

app.Run();
