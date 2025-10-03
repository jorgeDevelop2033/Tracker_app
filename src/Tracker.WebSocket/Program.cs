using Serilog;
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
app.Run();
