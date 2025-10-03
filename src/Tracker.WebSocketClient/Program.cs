using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5137/trackerHub")
    .Build();

await connection.StartAsync();

Console.WriteLine("✅ Conectado al WebSocket");

// Enviar coordenada de prueba
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await connection.InvokeAsync("SendCoordinate", new
{
    DeviceId = "TestConsolev1",
    Latitude = -33.4569,
    Longitude = -70.6483,
    Timestamp = DateTime.UtcNow
});

Console.WriteLine("📍 Coordenada enviada desde cliente console!");
