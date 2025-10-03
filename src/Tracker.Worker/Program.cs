using Tracker.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<GpsConsumer>();

var host = builder.Build();
host.Run();
