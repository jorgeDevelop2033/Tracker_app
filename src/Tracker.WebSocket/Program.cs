using Serilog;
using Tracker.WebSocket.Hubs;
using Tracker.WebSocket.Services;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/coordinates-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 🔹 Servicios
builder.Services.AddSignalR();
builder.Services.AddScoped<ITrackerService, TrackerService>();
//builder.Services.AddAutoMapper(typeof(Program));

// 🔹 Construcción de app
var app = builder.Build();

app.MapGet("/", () => "Tracker WebSocket is running 🚀");

// WebSocket (SignalR)
app.MapHub<TrackerHub>("/trackerHub");

app.Run();
