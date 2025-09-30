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

// 🔹 Configurar CORS para permitir acceso desde tu iPhone
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactNative", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

// 🔹 Usar CORS
app.UseCors("AllowReactNative");

app.MapGet("/", () => "Tracker WebSocket is running 🚀");

// 🔹 WebSocket (SignalR)
app.MapHub<TrackerHub>("/trackerHub");

app.Run(); // 👈 Escucha en todas las interfaces
