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
using Tracker.Domain.Entities;        // TarifaPortico, BandaHorario
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

// ===========================================================================
//  Carga de tarifas y horarios de banda (admin). Protegido con X-Internal-Key.
//  Resuelve el pórtico por Código. Idempotente: tarifas vía UpsertVigencia,
//  horarios reemplazando las ventanas del pórtico para ese DiaTipo.
// ===========================================================================
static bool InternalAuth(HttpContext http, string? key)
    => !string.IsNullOrEmpty(key) && http.Request.Headers["X-Internal-Key"].ToString() == key;

app.MapPost("/api/tarifas/bulk",
    async (TarifaBulkRow[] filas, HttpContext http, TrackerDbContext db,
           Tracker.Domain.Tarifas.ITarifaPorticoRepository repo,
           Tracker.Domain.Abstractions.IUnitOfWork uow, CancellationToken ct) =>
{
    if (!InternalAuth(http, internalKey)) return Results.Unauthorized();
    if (filas.Length == 0) return Results.BadRequest(new { error = "sin filas" });

    // Índice de pórticos por Código (puede haber repetidos por sentido → tomamos todos).
    var codigos = filas.Select(f => f.Codigo).Distinct().ToArray();
    var porticos = await db.Porticos.AsNoTracking()
        .Where(p => codigos.Contains(p.Codigo))
        .Select(p => new { p.Id, p.Codigo })
        .ToListAsync(ct);
    var porByCodigo = porticos.GroupBy(p => p.Codigo)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());

    int cargadas = 0; var noEncontrados = new List<string>();
    foreach (var f in filas)
    {
        if (!porByCodigo.TryGetValue(f.Codigo, out var ids)) { noEncontrados.Add(f.Codigo); continue; }
        if (f.ValorPorKm is null && f.ValorFijo is null) continue; // nada que cobrar

        foreach (var pid in ids)
        {
            await repo.UpsertVigenciaAsync(new TarifaPortico
            {
                Id = Guid.NewGuid(),
                PorticoId = pid,
                Categoria = f.Categoria,
                Banda = f.Banda,
                ValorPorKm = f.ValorPorKm,
                ValorFijo = f.ValorFijo,
                LongitudKmSnapshot = f.KmTramo,
                VigenteDesde = f.VigenteDesde ?? DateTime.UtcNow,
            }, ct);
            cargadas++;
        }
    }
    await uow.SaveChangesAsync(ct);
    return Results.Ok(new { cargadas, noEncontrados = noEncontrados.Distinct() });
}).WithName("BulkTarifas");

app.MapPost("/api/bandas-horario/bulk",
    async (BandaHorarioBulkRow[] filas, HttpContext http, TrackerDbContext db, CancellationToken ct) =>
{
    if (!InternalAuth(http, internalKey)) return Results.Unauthorized();
    if (filas.Length == 0) return Results.BadRequest(new { error = "sin filas" });

    var codigos = filas.Select(f => f.Codigo).Distinct().ToArray();
    var porticos = await db.Porticos.AsNoTracking()
        .Where(p => codigos.Contains(p.Codigo))
        .Select(p => new { p.Id, p.Codigo })
        .ToListAsync(ct);
    var porByCodigo = porticos.GroupBy(p => p.Codigo)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());

    int cargadas = 0; var noEncontrados = new List<string>();
    foreach (var f in filas)
    {
        if (!porByCodigo.TryGetValue(f.Codigo, out var ids)) { noEncontrados.Add(f.Codigo); continue; }
        if (!TimeOnly.TryParse(f.HoraInicio, out var ini) || !TimeOnly.TryParse(f.HoraFin, out var fin))
            continue;

        foreach (var pid in ids)
        {
            db.BandasHorario.Add(new BandaHorario
            {
                Id = Guid.NewGuid(),
                PorticoId = pid,
                DiaTipo = f.DiaTipo,
                HoraInicio = ini,
                HoraFin = fin,
                Banda = f.Banda,
            });
            cargadas++;
        }
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { cargadas, noEncontrados = noEncontrados.Distinct() });
}).WithName("BulkBandasHorario");

// Borra las ventanas de banda de un pórtico (para recargar sin duplicar).
app.MapDelete("/api/bandas-horario/{codigo}",
    async (string codigo, HttpContext http, TrackerDbContext db, CancellationToken ct) =>
{
    if (!InternalAuth(http, internalKey)) return Results.Unauthorized();
    var ids = await db.Porticos.Where(p => p.Codigo == codigo).Select(p => p.Id).ToListAsync(ct);
    var borradas = await db.BandasHorario.Where(b => ids.Contains(b.PorticoId)).ExecuteDeleteAsync(ct);
    return Results.Ok(new { borradas });
}).WithName("DeleteBandasHorario");

// ===========================================================================
//  Gasto del dispositivo: totales (día/semana/mes) y detalle de tránsitos.
// ===========================================================================

// Resumen agregado del gasto por período.
app.MapGet("/api/gastos/resumen",
    async (string deviceId, DateTime? from, DateTime? to, string? groupBy,
           TrackerDbContext db, CancellationToken ct) =>
{
    var toUtc = to ?? DateTime.UtcNow;
    var fromUtc = from ?? toUtc.AddDays(-30);

    var transitos = await db.Transitos.AsNoTracking()
        .Where(t => t.DeviceId == deviceId && t.Utc >= fromUtc && t.Utc <= toUtc)
        .Select(t => new { t.Utc, t.PrecioCalculado })
        .ToListAsync(ct);

    var total = transitos.Sum(t => t.PrecioCalculado);
    var modo = (groupBy ?? "dia").ToLowerInvariant();

    // Agrupación por período (clave local-ish basada en Utc; suficiente para reporte).
    static int IsoWeek(DateTime d) => System.Globalization.ISOWeek.GetWeekOfYear(d);
    var grupos = modo switch
    {
        "semana" => transitos.GroupBy(t => $"{t.Utc.Year}-W{IsoWeek(t.Utc):00}"),
        "mes" => transitos.GroupBy(t => $"{t.Utc.Year}-{t.Utc.Month:00}"),
        _ => transitos.GroupBy(t => t.Utc.ToString("yyyy-MM-dd")),
    };

    var detalle = grupos
        .Select(g => new { periodo = g.Key, transitos = g.Count(), total = g.Sum(x => x.PrecioCalculado) })
        .OrderBy(x => x.periodo);

    return Results.Ok(new
    {
        deviceId, from = fromUtc, to = toUtc, groupBy = modo,
        totalTransitos = transitos.Count, totalGasto = total,
        periodos = detalle
    });
}).WithName("GastoResumen");

// Detalle de tránsitos cobrados (con pórtico, banda y precio).
app.MapGet("/api/gastos/detalle",
    async (string deviceId, DateTime? from, DateTime? to, int? take,
           TrackerDbContext db, CancellationToken ct) =>
{
    var toUtc = to ?? DateTime.UtcNow;
    var fromUtc = from ?? toUtc.AddDays(-7);

    var data = await db.Transitos.AsNoTracking()
        .Where(t => t.DeviceId == deviceId && t.Utc >= fromUtc && t.Utc <= toUtc)
        .OrderByDescending(t => t.Utc)
        .Take(take ?? 500)
        .Select(t => new
        {
            t.Utc,
            t.PorticoId,
            Portico = t.Portico.Codigo,
            t.Portico.Autopista,
            Banda = t.Banda.ToString(),
            Categoria = t.Categoria.ToString(),
            Precio = t.PrecioCalculado
        })
        .ToListAsync(ct);

    return Results.Ok(data);
}).WithName("GastoDetalle");

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
