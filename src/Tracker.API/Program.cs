using Microsoft.EntityFrameworkCore;
using Tracker.Infrastructure.DependencyInjection;
using Tracker.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI (minimal en .NET 9)
builder.Services.AddOpenApi();

// EF Core + SQL Server + NetTopologySuite (registrado desde Infrastructure)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoints de ejemplo
app.MapGet("/health", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }))
   .WithName("Health");

// Mini endpoint para ver pórticos desde la BD
app.MapGet("/api/porticos", async (TrackerDbContext db) =>
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
            p.LongitudKm
            // Nota: Ubicacion/Corredor son geography; si quieres, expórtalos a WKT/WKB
        })
        .ToListAsync();

    return Results.Ok(data);
}).WithName("GetPorticos");

app.Run();
