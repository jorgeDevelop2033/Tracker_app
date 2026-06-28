#nullable enable
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Persistence;

namespace Tracker.Infrastructure.Seed
{
    /// <summary>
    /// Carga el catálogo de pórticos (datos reales de OpenStreetMap) en la BD.
    /// Idempotente: hace upsert por <see cref="Portico.OsmId"/>, por lo que puede
    /// ejecutarse en cada arranque sin duplicar ni perder ediciones manuales de
    /// campos no provenientes del catálogo.
    /// </summary>
    public static class PorticoSeeder
    {
        private const string ResourceName = "Tracker.Infrastructure.Seed.porticos_seed.json";

        // SRID 4326 = WGS84 (lat/lon GPS), el mismo que usa el resto del dominio.
        private static readonly GeometryFactory GeoFactory =
            NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// Devuelve cuántos pórticos se insertaron y cuántos se actualizaron.
        /// </summary>
        public static async Task<(int inserted, int updated)> SeedAsync(
            TrackerDbContext db, CancellationToken ct = default)
        {
            var registros = LoadEmbeddedCatalog();
            if (registros.Count == 0)
                return (0, 0);

            // Cargamos el catálogo existente indexado por OsmId (sólo los que tienen OsmId).
            var existentes = await db.Porticos
                .Where(p => p.OsmId != null)
                .ToDictionaryAsync(p => p.OsmId!.Value, ct);

            int inserted = 0, updated = 0;

            foreach (var r in registros)
            {
                if (r.OsmId == 0) continue; // sin clave natural -> se omite

                var punto = GeoFactory.CreatePoint(new Coordinate(r.Lon, r.Lat));

                if (existentes.TryGetValue(r.OsmId, out var actual))
                {
                    // Upsert: refrescamos los campos derivados del catálogo.
                    actual.Codigo = r.Codigo;
                    actual.Autopista = r.Autopista;
                    actual.Sentido = r.Sentido;
                    actual.Descripcion = r.Descripcion;
                    actual.CallesRef = r.CallesRef;
                    actual.Ubicacion = punto;
                    updated++;
                }
                else
                {
                    db.Porticos.Add(new Portico
                    {
                        Id = Guid.NewGuid(),
                        OsmId = r.OsmId,
                        Codigo = r.Codigo,
                        Autopista = r.Autopista,
                        Sentido = r.Sentido,
                        Descripcion = r.Descripcion,
                        CallesRef = r.CallesRef,
                        Ubicacion = punto,
                        Vigente = true,
                    });
                    inserted++;
                }
            }

            await db.SaveChangesAsync(ct);
            return (inserted, updated);
        }

        private static List<PorticoSeedRow> LoadEmbeddedCatalog()
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"No se encontró el recurso embebido '{ResourceName}'. " +
                    "Verifica que Seed/porticos_seed.json esté marcado como EmbeddedResource.");

            return JsonSerializer.Deserialize<List<PorticoSeedRow>>(stream, JsonOpts)
                   ?? new List<PorticoSeedRow>();
        }

        private sealed class PorticoSeedRow
        {
            [JsonPropertyName("OsmId")] public long OsmId { get; set; }
            [JsonPropertyName("Codigo")] public string Codigo { get; set; } = default!;
            [JsonPropertyName("Autopista")] public string Autopista { get; set; } = default!;
            [JsonPropertyName("Sentido")] public string Sentido { get; set; } = "Bidireccional";
            [JsonPropertyName("Descripcion")] public string Descripcion { get; set; } = default!;
            [JsonPropertyName("CallesRef")] public string? CallesRef { get; set; }
            [JsonPropertyName("Lat")] public double Lat { get; set; }
            [JsonPropertyName("Lon")] public double Lon { get; set; }
        }
    }
}
