// Program.cs (Tracker.API)
// Responsabilidades de este servicio:
//   1. REST de lectura para el dashboard (última posición, recorrido histórico, pórticos).
//   2. SignalR LiveHub: reemite en vivo las posiciones que el Worker le empuja por HTTP.
// La ingesta del móvil sigue en Tracker.WebSocket → Kafka → Worker.

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tracker.API.Contracts;
using Tracker.API.Hubs;
using Tracker.Application.Services;   // IViajeService
using Tracker.Contracts.Enums;        // EstadoViaje
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;        // TarifaPortico, BandaHorario, Vehiculo, Viaje
using Tracker.Domain.Vehiculos;       // IVehiculoRepository, IAsignacionDispositivoRepository
using Tracker.Domain.Viajes;          // IViajeRepository
using Tracker.Infrastructure.DependencyInjection; // AddInfrastructure(...)
using Tracker.Infrastructure.Persistence;         // TrackerDbContext
using Tracker.Infrastructure.Repositories;        // GpsFixRepository

var builder = WebApplication.CreateBuilder(args);

// ===== OpenAPI (.NET 9 minimal) =====
builder.Services.AddOpenApi();

// Enums como string en el JSON de los minimal endpoints (acepta "C1","TBP","Laboral").
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

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

    // Índice de pórticos por (Código, Autopista). Los códigos colisionan entre
    // concesiones, por eso se puede filtrar opcionalmente por autopista.
    var codigos = filas.Select(f => f.Codigo).Distinct().ToArray();
    var porticos = await db.Porticos.AsNoTracking()
        .Where(p => codigos.Contains(p.Codigo))
        .Select(p => new { p.Id, p.Codigo, p.Autopista })
        .ToListAsync(ct);

    Guid[] Resolver(string codigo, string? autopista) => porticos
        .Where(p => p.Codigo == codigo && (autopista == null || p.Autopista == autopista))
        .Select(p => p.Id).ToArray();

    int cargadas = 0; var noEncontrados = new List<string>();
    foreach (var f in filas)
    {
        var ids = Resolver(f.Codigo, f.Autopista);
        if (ids.Length == 0) { noEncontrados.Add(f.Autopista is null ? f.Codigo : $"{f.Codigo}@{f.Autopista}"); continue; }
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
        .Select(p => new { p.Id, p.Codigo, p.Autopista })
        .ToListAsync(ct);

    Guid[] Resolver(string codigo, string? autopista) => porticos
        .Where(p => p.Codigo == codigo && (autopista == null || p.Autopista == autopista))
        .Select(p => p.Id).ToArray();

    int cargadas = 0; var noEncontrados = new List<string>();
    foreach (var f in filas)
    {
        var ids = Resolver(f.Codigo, f.Autopista);
        if (ids.Length == 0) { noEncontrados.Add(f.Autopista is null ? f.Codigo : $"{f.Codigo}@{f.Autopista}"); continue; }
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
// Opcional ?autopista=... para no afectar pórticos homónimos de otras concesiones.
app.MapDelete("/api/bandas-horario/{codigo}",
    async (string codigo, string? autopista, HttpContext http, TrackerDbContext db, CancellationToken ct) =>
{
    if (!InternalAuth(http, internalKey)) return Results.Unauthorized();
    var ids = await db.Porticos
        .Where(p => p.Codigo == codigo && (autopista == null || p.Autopista == autopista))
        .Select(p => p.Id).ToListAsync(ct);
    var borradas = await db.BandasHorario.Where(b => ids.Contains(b.PorticoId)).ExecuteDeleteAsync(ct);
    return Results.Ok(new { borradas });
}).WithName("DeleteBandasHorario");

// Re-etiqueta la Autopista de pórticos por OsmId (limpieza del catálogo sin re-seed).
// Body: [{ "osmId": 123, "autopista": "Costanera Norte" }, ...]
app.MapPost("/api/porticos/reetiquetar",
    async (PorticoReetiquetaRow[] filas, HttpContext http, TrackerDbContext db, CancellationToken ct) =>
{
    if (!InternalAuth(http, internalKey)) return Results.Unauthorized();
    if (filas.Length == 0) return Results.BadRequest(new { error = "sin filas" });

    int actualizados = 0; var noEncontrados = new List<long>();
    foreach (var f in filas)
    {
        var n = await db.Porticos
            .Where(p => p.OsmId == f.OsmId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Autopista, f.Autopista), ct);
        if (n == 0) noEncontrados.Add(f.OsmId); else actualizados += n;
    }
    return Results.Ok(new { actualizados, noEncontrados });
}).WithName("ReetiquetarPorticos");

// ===========================================================================
//  Gasto del dispositivo: totales (día/semana/mes) y detalle de tránsitos.
// ===========================================================================

// Resumen agregado del gasto por período.
//
// Agrupa por FECHA LOCAL de Chile, no por UTC. Chile va UTC-4/-3, así que todo
// tránsito posterior a las ~20:00 locales cae en el día UTC siguiente: agrupar
// por Utc mandaba los peajes de la tarde-noche al día equivocado (y en los
// cortes de mes, al mes equivocado). Como el Worker ya persiste FechaLocal, la
// agrupación se hace en la BD sobre una columna indexada en vez de traerse
// todos los tránsitos a memoria.
//
// Filtra por vehiculoId (preferido) o deviceId (compatibilidad).
app.MapGet("/api/gastos/resumen",
    async (Guid? vehiculoId, string? deviceId, DateOnly? desde, DateOnly? hasta, string? groupBy,
           TrackerDbContext db, CancellationToken ct) =>
{
    if (vehiculoId is null && string.IsNullOrWhiteSpace(deviceId))
        return Results.BadRequest(new { error = "Indica vehiculoId o deviceId." });

    var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    var hastaLocal = hasta ?? hoy;
    var desdeLocal = desde ?? hastaLocal.AddDays(-30);

    var q = db.Transitos.AsNoTracking()
        .Where(t => t.FechaLocal >= desdeLocal && t.FechaLocal <= hastaLocal);

    q = vehiculoId is Guid v
        ? q.Where(t => t.VehiculoId == v)
        : q.Where(t => t.DeviceId == deviceId);

    var modo = (groupBy ?? "dia").ToLowerInvariant();

    // La agregación ocurre en SQL. Para "dia" y "autopista" se agrupa por columna
    // directa; para mes/semana se derivan del año/mes de la fecha local.
    object periodos = modo switch
    {
        "mes" => await q
            .GroupBy(t => new { t.FechaLocal.Year, t.FechaLocal.Month })
            .Select(g => new
            {
                periodo = g.Key.Year + "-" + (g.Key.Month < 10 ? "0" + g.Key.Month : g.Key.Month.ToString()),
                transitos = g.Count(),
                total = g.Sum(x => x.PrecioCalculado)
            })
            .OrderBy(x => x.periodo)
            .ToListAsync(ct),

        "autopista" => await q
            .GroupBy(t => t.AutopistaSnapshot)
            .Select(g => new
            {
                periodo = g.Key ?? "(sin autopista)",
                transitos = g.Count(),
                total = g.Sum(x => x.PrecioCalculado)
            })
            .OrderByDescending(x => x.total)
            .ToListAsync(ct),

        _ => await q
            .GroupBy(t => t.FechaLocal)
            .Select(g => new
            {
                periodo = g.Key.ToString(),
                transitos = g.Count(),
                total = g.Sum(x => x.PrecioCalculado)
            })
            .OrderBy(x => x.periodo)
            .ToListAsync(ct)
    };

    var totalTransitos = await q.CountAsync(ct);
    var totalGasto = await q.SumAsync(t => t.PrecioCalculado, ct);

    return Results.Ok(new
    {
        vehiculoId,
        deviceId,
        desde = desdeLocal,
        hasta = hastaLocal,
        groupBy = modo,
        totalTransitos,
        totalGasto,
        periodos
    });
}).WithName("GastoResumen");

// Detalle de tránsitos cobrados, en hora local y con los datos congelados al
// momento del paso (no por join en vivo: reetiquetar un pórtico no debe
// reescribir una conciliación ya cerrada).
//
// Paginado por keyset sobre (Utc, Id): con `antesDe` se pide la página
// siguiente. Take con Skip degrada al avanzar y no soporta bien años de
// historial.
app.MapGet("/api/gastos/detalle",
    async (Guid? vehiculoId, string? deviceId, DateOnly? desde, DateOnly? hasta,
           DateTime? antesDe, int? take,
           TrackerDbContext db, CancellationToken ct) =>
{
    if (vehiculoId is null && string.IsNullOrWhiteSpace(deviceId))
        return Results.BadRequest(new { error = "Indica vehiculoId o deviceId." });

    var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    var hastaLocal = hasta ?? hoy;
    var desdeLocal = desde ?? hastaLocal.AddDays(-7);
    var limite = Math.Clamp(take ?? 200, 1, 1000);

    var q = db.Transitos.AsNoTracking()
        .Where(t => t.FechaLocal >= desdeLocal && t.FechaLocal <= hastaLocal);

    q = vehiculoId is Guid v
        ? q.Where(t => t.VehiculoId == v)
        : q.Where(t => t.DeviceId == deviceId);

    if (antesDe is DateTime cursor)
        q = q.Where(t => t.Utc < cursor);

    var data = await q
        .OrderByDescending(t => t.Utc)
        .Take(limite)
        .Select(t => new TransitoDetalleDto(
            t.Id,
            t.FechaLocal,
            t.HoraLocal,
            t.PorticoCodigoSnapshot,
            t.AutopistaSnapshot,
            t.SentidoSnapshot,
            t.Banda.ToString(),
            t.Categoria.ToString(),
            t.DiaTipo.ToString(),
            t.PrecioCalculado,
            t.EstadoConciliacion.ToString()))
        .ToListAsync(ct);

    return Results.Ok(new
    {
        items = data,
        // Cursor para la página siguiente; null cuando ya no hay más.
        siguienteCursor = data.Count == limite
            ? await q.OrderByDescending(t => t.Utc).Skip(limite - 1).Select(t => (DateTime?)t.Utc).FirstOrDefaultAsync(ct)
            : null
    });
}).WithName("GastoDetalle");

// ===========================================================================
//  Vehículos y asignación de dispositivos
// ===========================================================================

app.MapGet("/api/vehiculos", async (bool? soloActivos, IVehiculoRepository repo,
                                    IAsignacionDispositivoRepository asignaciones,
                                    CancellationToken ct) =>
{
    var vehiculos = await repo.ListAsync(soloActivos ?? true, ct);

    var salida = new List<VehiculoDto>(vehiculos.Count);
    foreach (var v in vehiculos)
    {
        var abiertas = await asignaciones.ListByVehiculoAsync(v.Id, ct);
        var actual = abiertas.FirstOrDefault(a => a.HastaUtc is null)?.DeviceId;

        salida.Add(new VehiculoDto(v.Id, v.Patente, v.Alias, v.Categoria.ToString(),
                                   v.Marca, v.Modelo, v.Anio, v.Activo, actual));
    }

    return Results.Ok(salida);
}).WithName("ListarVehiculos");

app.MapPost("/api/vehiculos", async (CrearVehiculoRequest req, IVehiculoRepository repo,
                                     IUnitOfWork uow, CancellationToken ct) =>
{
    var patente = Vehiculo.NormalizarPatente(req.Patente);
    if (string.IsNullOrWhiteSpace(patente))
        return Results.BadRequest(new { error = "La patente es obligatoria." });

    if (await repo.GetByPatenteAsync(patente, ct) is not null)
        return Results.Conflict(new { error = $"Ya existe un vehículo con patente {patente}." });

    var vehiculo = new Vehiculo
    {
        Id = Guid.NewGuid(),
        Patente = patente,
        Alias = req.Alias,
        Categoria = req.Categoria,
        Marca = req.Marca,
        Modelo = req.Modelo,
        Anio = req.Anio
    };

    await repo.AddAsync(vehiculo, ct);
    await uow.SaveChangesAsync(ct);

    return Results.Created($"/api/vehiculos/{vehiculo.Id}",
        new VehiculoDto(vehiculo.Id, vehiculo.Patente, vehiculo.Alias, vehiculo.Categoria.ToString(),
                        vehiculo.Marca, vehiculo.Modelo, vehiculo.Anio, vehiculo.Activo, null));
}).WithName("CrearVehiculo");

// Asigna un dispositivo al vehículo. Cierra la asignación abierta anterior del
// mismo device: el teléfono no puede estar en dos autos a la vez, y el corte
// deja intacta la atribución de los tránsitos ya registrados.
app.MapPost("/api/vehiculos/{id:guid}/dispositivos",
    async (Guid id, AsignarDispositivoRequest req,
           IVehiculoRepository vehiculos, IAsignacionDispositivoRepository asignaciones,
           IUnitOfWork uow, CancellationToken ct) =>
{
    if (await vehiculos.GetByIdAsync(id, ct) is null)
        return Results.NotFound(new { error = "Vehículo no encontrado." });

    if (string.IsNullOrWhiteSpace(req.DeviceId))
        return Results.BadRequest(new { error = "DeviceId es obligatorio." });

    var desde = req.DesdeUtc ?? DateTime.UtcNow;

    var previa = await asignaciones.GetAbiertaAsync(req.DeviceId, ct);
    if (previa is not null)
    {
        if (previa.VehiculoId == id)
            return Results.Ok(new AsignacionDto(previa.Id, previa.DeviceId, previa.DesdeUtc, previa.HastaUtc, previa.Nota));

        previa.HastaUtc = desde;
    }

    var nueva = new AsignacionDispositivo
    {
        Id = Guid.NewGuid(),
        DeviceId = req.DeviceId,
        VehiculoId = id,
        DesdeUtc = desde,
        Nota = req.Nota
    };

    await asignaciones.AddAsync(nueva, ct);
    await uow.SaveChangesAsync(ct);

    return Results.Ok(new AsignacionDto(nueva.Id, nueva.DeviceId, nueva.DesdeUtc, nueva.HastaUtc, nueva.Nota));
}).WithName("AsignarDispositivo");

app.MapGet("/api/vehiculos/{id:guid}/dispositivos",
    async (Guid id, IAsignacionDispositivoRepository repo, CancellationToken ct) =>
{
    var lista = await repo.ListByVehiculoAsync(id, ct);
    return Results.Ok(lista.Select(a => new AsignacionDto(a.Id, a.DeviceId, a.DesdeUtc, a.HastaUtc, a.Nota)));
}).WithName("HistorialDispositivos");

// ===========================================================================
//  Viajes
// ===========================================================================

app.MapPost("/api/viajes/iniciar",
    async (IniciarViajeRequest req, IViajeService viajes, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.DeviceId))
        return Results.BadRequest(new { error = "DeviceId es obligatorio." });

    try
    {
        var viaje = await viajes.IniciarAsync(
            req.DeviceId, req.VehiculoId, req.Nombre, req.Utc ?? DateTime.UtcNow, ct);

        return Results.Ok(AViajeResumen(viaje));
    }
    catch (InvalidOperationException ex)
    {
        // Device sin vehículo asignado: es un error del cliente, no del servidor.
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("IniciarViaje");

app.MapPost("/api/viajes/{id:guid}/finalizar",
    async (Guid id, FinalizarViajeRequest? req, IViajeService viajes, CancellationToken ct) =>
{
    var viaje = await viajes.FinalizarAsync(
        id, req?.Utc ?? DateTime.UtcNow, EstadoViaje.Finalizado, ct);

    return viaje is null
        ? Results.NotFound(new { error = "Viaje no encontrado." })
        : Results.Ok(AViajeResumen(viaje));
}).WithName("FinalizarViaje");

// Viaje abierto de un device (la app lo consulta al reabrirse para retomar).
app.MapGet("/api/viajes/en-curso",
    async (string deviceId, IViajeService viajes, CancellationToken ct) =>
{
    var viaje = await viajes.ObtenerEnCursoAsync(deviceId, ct);
    return viaje is null ? Results.NoContent() : Results.Ok(AViajeResumen(viaje));
}).WithName("ViajeEnCurso");

// Historial de viajes del vehículo, paginado.
app.MapGet("/api/vehiculos/{id:guid}/viajes",
    async (Guid id, DateTime? desde, DateTime? hasta, int? page, int? pageSize,
           IViajeRepository repo, CancellationToken ct) =>
{
    var take = Math.Clamp(pageSize ?? 50, 1, 200);
    var skip = Math.Max(0, (page ?? 1) - 1) * take;

    var total = await repo.CountByVehiculoAsync(id, desde, hasta, ct);
    var lista = await repo.ListByVehiculoAsync(id, desde, hasta, skip, take, ct);

    return Results.Ok(new
    {
        total,
        page = page ?? 1,
        pageSize = take,
        items = lista.Select(AViajeResumen)
    });
}).WithName("HistorialViajes");

// Detalle del viaje: cabecera, tránsitos cobrados y corte por autopista.
app.MapGet("/api/viajes/{id:guid}",
    async (Guid id, IViajeRepository repo, TrackerDbContext db, CancellationToken ct) =>
{
    var viaje = await repo.GetByIdAsync(id, ct);
    if (viaje is null) return Results.NotFound();

    var transitos = await db.Transitos.AsNoTracking()
        .Where(t => t.ViajeId == id)
        .OrderBy(t => t.Utc)
        .Select(t => new TransitoDetalleDto(
            t.Id,
            t.FechaLocal,
            t.HoraLocal,
            t.PorticoCodigoSnapshot,
            t.AutopistaSnapshot,
            t.SentidoSnapshot,
            t.Banda.ToString(),
            t.Categoria.ToString(),
            t.DiaTipo.ToString(),
            t.PrecioCalculado,
            t.EstadoConciliacion.ToString()))
        .ToListAsync(ct);

    var porAutopista = transitos
        .GroupBy(t => t.Autopista ?? "(sin autopista)")
        .Select(g => new TotalAutopistaDto(g.Key, g.Count(), g.Sum(x => x.Precio)))
        .OrderByDescending(x => x.Total)
        .ToList();

    return Results.Ok(new ViajeDetalleDto(AViajeResumen(viaje), transitos, porAutopista));
}).WithName("DetalleViaje");

// Recorrido del viaje. Devuelve la ruta simplificada si el viaje ya está cerrado
// (es ~20x más chica y basta para dibujar); si sigue abierto, los fixes crudos.
app.MapGet("/api/viajes/{id:guid}/ruta",
    async (Guid id, IViajeRepository repo, IGpsFixRepository fixes, CancellationToken ct) =>
{
    var viaje = await repo.GetByIdAsync(id, ct);
    if (viaje is null) return Results.NotFound();

    if (viaje.RutaSimplificada is not null)
    {
        var puntos = viaje.RutaSimplificada.Coordinates
            .Select(c => new { lat = c.Y, lon = c.X });

        return Results.Ok(new { viajeId = id, origen = "simplificada", puntos });
    }

    var crudos = await fixes.ListByViajeAsync(id, ct: ct);
    return Results.Ok(new
    {
        viajeId = id,
        origen = "fixes",
        puntos = crudos.Select(f => new { lat = f.Lat, lon = f.Lon, utc = f.Utc })
    });
}).WithName("RutaViaje");

static ViajeResumenDto AViajeResumen(Viaje v) => new(
    v.Id, v.VehiculoId, v.DeviceId, v.InicioUtc, v.FinUtc, v.FechaLocalInicio,
    v.Estado.ToString(), v.Nombre, v.CantidadTransitos, v.TotalGasto, v.DistanciaKm);

// ===== SignalR endpoint =====
app.MapHub<LiveHub>("/liveHub");

// ===== Auto-migración al arranque =====
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                     .CreateLogger("Startup");
    var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();

    // Aviso temprano del desajuste EnsureCreated/Migrate: si el Worker creó la
    // BD, las tablas existen pero __EFMigrationsHistory está vacía y la
    // migración va a chocar contra objetos ya existentes.
    var aplicadas = (await db.Database.GetAppliedMigrationsAsync()).ToList();
    var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();

    if (aplicadas.Count == 0 && await db.Database.CanConnectAsync())
        logger.LogWarning(
            "⚠️ La BD no registra ninguna migración aplicada. Si las tablas ya existen, " +
            "fueron creadas con EnsureCreated() y hay que sembrar __EFMigrationsHistory antes de migrar.");

    if (pendientes.Count > 0)
        logger.LogInformation("Migraciones pendientes: {Pendientes}", string.Join(", ", pendientes));

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("✅ Migraciones aplicadas correctamente.");
    }
    catch (Exception ex)
    {
        // Antes esto solo se logueaba y la API arrancaba igual: quedaba sirviendo
        // tráfico contra un esquema a medio migrar, devolviendo errores raros por
        // endpoint en vez de un fallo claro. En una app de conciliación, datos
        // servidos desde un esquema inconsistente son peores que no responder.
        logger.LogCritical(ex, "❌ Error aplicando migraciones. La API no va a arrancar.");
        throw;
    }
}

app.Run();
