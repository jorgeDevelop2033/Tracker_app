#nullable enable
using NetTopologySuite.Geometries;

namespace Tracker.Domain.Entities;

public class Portico : BaseEntity
{
    public string Codigo { get; set; } = default!;
    public string Autopista { get; set; } = default!;
    public string Sentido { get; set; } = default!;
    public string Descripcion { get; set; } = default!;
    public string? CallesRef { get; set; }
    public decimal? LongitudKm { get; set; }

    // SRID 4326 (WGS84)
    public Point? Ubicacion { get; set; }
    public LineString? Corredor { get; set; }

    public bool Vigente { get; set; } = true;

    public ICollection<TarifaPortico> Tarifas { get; set; } = new List<TarifaPortico>();
}
