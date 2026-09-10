using Serilog;
using Tracker.Contracts;
using Tracker.WebSocket.DTOs;
using Tracker.WebSocket.Hubs;
using Tracker.WebSocket.Services;
using Tracker.WebSocket.Messaging; // 👈 agrega esto

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/coordinates-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddSignalR();
builder.Services.AddScoped<ITrackerService, TrackerService>();
builder.Services.AddSingleton<IKafkaPublisher, KafkaPublisher>();

builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactNative", policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true));
});

var app = builder.Build();
app.UseCors("AllowReactNative");

app.MapGet("/", () => "Tracker WebSocket is running 🚀");
app.MapHub<TrackerHub>("/trackerHub");

// Ingesta REST por lotes, para la tarea de ubicación en segundo plano del móvil.
//
// Por qué existe además del hub: iOS despierta la tarea de background unos
// pocos segundos y entrega las posiciones acumuladas de golpe. En esa ventana no
// da tiempo a levantar un WebSocket (negotiate + upgrade + handshake), así que
// los fixes se perdían. Un POST stateless sí cabe.
//
// Cuelga de /trackerHub a propósito: nginx enruta ese prefijo a este servicio,
// mientras que /api/ va al tracker-api, que no tiene productor de Kafka.
//
// Publica en el MISMO tópico y con el mismo mapeo que TrackerHub.SendCoordinate,
// así que para el Worker es indistinguible de una coordenada llegada por el hub.
app.MapPost("/trackerHub/coordinates", async (
    CoordinateDto[] coordenadas,
    IKafkaPublisher bus,
    IConfiguration cfg,
    ILoggerFactory logs,
    CancellationToken ct) =>
{
    var log = logs.CreateLogger("IngestaRest");

    if (coordenadas is null || coordenadas.Length == 0)
        return Results.BadRequest(new { error = "Se esperaba un arreglo con al menos una coordenada." });

    // Tope defensivo: un lote enorme mantendría la petición abierta más de lo
    // que dura la ventana de background del teléfono, y no llegaría a completarse.
    if (coordenadas.Length > 500)
        return Results.BadRequest(new { error = "Máximo 500 coordenadas por lote." });

    var topic = cfg["Kafka:Topic"] ?? "tracker.gps.events";
    var publicadas = 0;

    // En orden cronológico: el detector de pórticos une fixes consecutivos para
    // formar el tramo recorrido, y desordenarlos rompería esa geometría.
    foreach (var c in coordenadas.OrderBy(x => x.Timestamp))
    {
        if (string.IsNullOrWhiteSpace(c.DeviceId)) continue;

        var ev = new GpsEvent(
            DeviceId: c.DeviceId,
            Lat: c.Latitude,
            Lon: c.Longitude,
            SpeedKph: c.SpeedKph,
            HeadingDeg: c.HeadingDeg,
            Utc: c.Timestamp,
            AccuracyM: c.AccuracyM
        );

        var proto = ev.ToProto();
        try
        {
            await bus.PublishAsync(topic, proto.DeviceId, proto);
            publicadas++;
        }
        catch (Exception ex)
        {
            // Se registra y se sigue con el resto del lote: perder un fix es
            // preferible a descartar el viaje entero por un fallo puntual.
            log.LogError(ex, "❌ Falló PublishAsync (topic={Topic}, Device={Device})", topic, c.DeviceId);
        }
    }

    log.LogInformation("📥 Lote REST: {Publicadas}/{Recibidas} coordenadas publicadas en {Topic}",
        publicadas, coordenadas.Length, topic);

    // 202: el teléfono no debe esperar a que el Worker las procese.
    return Results.Accepted(value: new { recibidas = coordenadas.Length, publicadas });
});

app.Run();
