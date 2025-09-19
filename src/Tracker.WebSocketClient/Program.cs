using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://192.168.10.32:5137/trackerHub")
    .Build();

await connection.StartAsync();

Console.WriteLine("✅ Conectado al WebSocket");

// Enviar coordenada de prueba
await connection.InvokeAsync("SendCoordinate", new
{
    DeviceId = "TestConsole",
    Latitude = -33.4569,
    Longitude = -70.6483,
    Timestamp = DateTime.UtcNow
});

Console.WriteLine("📍 Coordenada enviada desde cliente console!");
