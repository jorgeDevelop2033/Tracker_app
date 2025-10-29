#nullable enable
using NetTopologySuite.Geometries;
using Tracker.Domain.Entities;

namespace Tracker.Domain.Repositories;

public interface IDeteccionPorticoRepository : IRepository<PasoPorPortico>, IReadRepository<PasoPorPortico>
{
    Task RegistrarAsync(PasoPorPortico deteccion, CancellationToken ct = default);
    Task<bool> ExistePasoAsync(Guid deviceId, Guid porticoId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default);
}

public class PasoPorPortico : BaseEntity
{
    public Guid DeviceId { get; set; }
    public Guid PorticoId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public Point? Posicion { get; set; } // ubicación reportada al cruzar
    public double DistanciaMetros { get; set; } // distancia al pórtico en el match
}
