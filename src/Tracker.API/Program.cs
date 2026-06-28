// Program.cs (Tracker.API)
// Responsabilidades de este servicio:
//   1. REST de lectura para el dashboard (última posición, recorrido histórico, pórticos).
//   2. SignalR LiveHub: reemite en vivo las posiciones que el Worker le empuja por HTTP.
// La ingesta del móvil sigue en Tracker.WebSocket → Kafka → Worker.

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tracker.API.Contracts;
using Tracker.API.Hubs;
using Tracker.Domain.Abstractions;
using Tracker.Infrastructure.DependencyInjection; // AddInfrastructure(...)
using Tracker.Infrastructure.Persistence;         // TrackerDbContext
using Tracker.Infrastructure.Repositories;        // GpsFixRepository

var builder = WebApplication.CreateBuilder(args);

// ===== OpenAPI (.NET 9 minimal) =====
builder.Services.AddOpenApi();

// ===== SignalR (broadcast en vivo al dashboard) =====
builder.Services.AddSignalR();

// ===== CORS (dashboard Angular) =====
// Orígenes configurables por Cors:AllowedOrigins (coma-separados) para producción.
// Si no se configura, se usan los de desarrollo local.
const string DashboardCors = "AllowDashboard";
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:4200,https://localhost:4200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy(DashboardCors, p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // necesario para SignalR

// ===== Infra (EF Core + SQL Server + NTS) =====
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddDbContext<TrackerDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("TrackerDb");
    opt.UseSqlServer(cs, sql =>
    {
        sql.UseNetTopologySuite();
        // Las migraciones viven en Tracker.Infrastructure (NO en Tracker.API).
        sql.MigrationsAssembly("Tracker.Infrastructure");
    });
});

// AddInfrastructure no registra el repo de GpsFix; la API lo necesita para las lecturas.
builder.Services.AddScoped<IGpsFixRepository, GpsFixRepository>();

var app = builder.Build();

// ===== OpenAPI =====
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(DashboardCors);

// ===== Health =====
app.MapGet("/health", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }))
   .WithName("Health");

// ===========================================================================
//  Pórticos
// ===========================================================================

// Listado simple (admin / debug).
app.MapGet("/api/porticos", async (TrackerDbContext db, CancellationToken ct) =>
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
            Lat = p.Ubicacion != null ? p.Ubicacion.Y : (double?)null,
            Lon = p.Ubicacion != null ? p.Ubicacion.X : (double?)null
        })
        .ToListAsync(ct);

    return Results.Ok(data);
}).WithName("GetPorticos");

// GeoJSON FeatureCollection para pintar los pórticos directo en el mapa.
app.MapGet("/api/porticos/geojson", async (TrackerDbContext db, CancellationToken ct) =>
{
    var porticos = await db.Porticos
        .AsNoTracking()
        .Where(p => p.Ubicacion != null)
        .Select(p => new
        {
            p.Codigo,
            p.Autopista,
            p.Sentido,
            p.Descripcion,
            Lon = p.Ubicacion!.X,
            Lat = p.Ubicacion!.Y
        })
        .ToListAsync(ct);

    var features = porticos.Select(p => new
    {
        type = "Feature",
        geometry = new { type = "Point", coordinates = new[] { p.Lon, p.Lat } },
        properties = new { p.Codigo, p.Autopista, p.Sentido, p.Descripcion }
    });

    return Results.Ok(new { type = "FeatureCollection", features });
}).WithName("GetPorticosGeoJson");

// ===========================================================================
//  Posiciones de dispositivos (lectura para el dashboard)
// ===========================================================================

// Última posición conocida del dispositivo.
app.MapGet("/api/devices/{id}/last",
    async (string id, IGpsFixRepository repo, CancellationToken ct) =>
{
    var fix = await repo.GetLastByDeviceAsync(id, ct);
    if (fix is null) return Results.NotFound();

    return Results.Ok(new LivePositionDto(
        fix.DeviceId, fix.Lat, fix.Lon,
        fix.SpeedKph, fix.HeadingDeg, fix.AccuracyM, fix.Utc));
}).WithName("GetDeviceLast");

// Recorrido histórico en una ventana de tiempo (para dibujar la polyline).
app.MapGet("/api/devices/{id}/track",
    async (string id, DateTime? from, DateTime? to, int? take,
           IGpsFixRepository repo, CancellationToken ct) =>
{
    var toUtc = (to ?? DateTime.UtcNow);
    var fromUtc = (from ?? toUtc.AddHours(-1));

    var fixes = await repo.ListByDeviceAndUtcRangeAsync(
        id, fromUtc, toUtc, take ?? 1000, ct);

    var points = fixes
        .OrderBy(f => f.Utc)
        .Select(f => new LivePositionDto(
            f.DeviceId, f.Lat, f.Lon,
            f.SpeedKph, f.HeadingDeg, f.AccuracyM, f.Utc));

    return Results.Ok(points);
}).WithName("GetDeviceTrack");

// ===========================================================================
//  Endpoint INTERNO: el Worker empuja aquí cada fix persistido y la API
//  lo reemite por SignalR al grupo del dispositivo.
//  Protegido con API key compartida (header X-Internal-Key). Solo el Worker
//  la conoce; sin ella, 401. Configurar InternalApi:Key igual en API y Worker.
// ===========================================================================
var internalKey = builder.Configuration["InternalApi:Key"];

app.MapPost("/internal/live",
    async (LivePositionDto pos, HttpContext http, IHubContext<LiveHub> hub, CancellationToken ct) =>
{
    // Validación de API key. Si no hay key configurada, se rechaza todo (fail-closed).
    var provided = http.Request.Headers["X-Internal-Key"].ToString();
    if (string.IsNullOrEmpty(internalKey) || provided != internalKey)
        return Results.Unauthorized();

    await hub.Clients
        .Group(LiveHub.GroupFor(pos.DeviceId))
        .SendAsync("position", pos, ct);
    return Results.Accepted();
}).WithName("PushLivePosition");

// Tránsito (paso por pórtico) -> evento "transito" al grupo del dispositivo.
app.MapPost("/internal/transito",
    async (TransitoEventDto ev, HttpContext http, IHubContext<LiveHub> hub, CancellationToken ct) =>
{
    var provided = http.Request.Headers["X-Internal-Key"].ToString();
    if (string.IsNullOrEmpty(internalKey) || provided != internalKey)
        return Results.Unauthorized();

    await hub.Clients
        .Group(LiveHub.GroupFor(ev.DeviceId))
        .SendAsync("transito", ev, ct);
    return Results.Accepted();
}).WithName("PushTransito");

// ===== SignalR endpoint =====
app.MapHub<LiveHub>("/liveHub");

// ===== Auto-migración al arranque =====
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                     .CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("✅ Migraciones aplicadas correctamente.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error aplicando migraciones.");
    }
}

app.Run();
