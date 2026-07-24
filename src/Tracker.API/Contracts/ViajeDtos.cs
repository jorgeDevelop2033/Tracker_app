#nullable enable

namespace Tracker.API.Contracts
{
    public sealed record IniciarViajeRequest(
        string DeviceId,
        Guid? VehiculoId,
        string? Nombre,
        DateTime? Utc);

    public sealed record FinalizarViajeRequest(DateTime? Utc);

    /// <summary>Fila del listado de viajes. Sin geometría, para que la lista sea liviana.</summary>
    public sealed record ViajeResumenDto(
        Guid Id,
        Guid VehiculoId,
        string DeviceId,
        DateTime InicioUtc,
        DateTime? FinUtc,
        DateOnly FechaLocalInicio,
        string Estado,
        string? Nombre,
        int CantidadTransitos,
        decimal TotalGasto,
        double DistanciaKm);

    /// <summary>Detalle de un viaje: cabecera + tránsitos + corte por autopista.</summary>
    public sealed record ViajeDetalleDto(
        ViajeResumenDto Viaje,
        IReadOnlyList<TransitoDetalleDto> Transitos,
        IReadOnlyList<TotalAutopistaDto> PorAutopista);

    public sealed record TransitoDetalleDto(
        Guid Id,
        DateOnly Fecha,
        TimeOnly Hora,
        string? Portico,
        string? Autopista,
        string? Sentido,
        string Banda,
        string Categoria,
        string DiaTipo,
        decimal Precio,
        string EstadoConciliacion);

    public sealed record TotalAutopistaDto(string Autopista, int Transitos, decimal Total);
}
