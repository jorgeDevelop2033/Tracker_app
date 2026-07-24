#nullable enable
using Tracker.Contracts.Enums;

namespace Tracker.API.Contracts
{
    public sealed record CrearVehiculoRequest(
        string Patente,
        string? Alias,
        VehicleCategory Categoria,
        string? Marca,
        string? Modelo,
        int? Anio);

    public sealed record VehiculoDto(
        Guid Id,
        string Patente,
        string? Alias,
        string Categoria,
        string? Marca,
        string? Modelo,
        int? Anio,
        bool Activo,
        string? DeviceActual);

    public sealed record AsignarDispositivoRequest(string DeviceId, DateTime? DesdeUtc, string? Nota);

    public sealed record AsignacionDto(
        Guid Id,
        string DeviceId,
        DateTime DesdeUtc,
        DateTime? HastaUtc,
        string? Nota);
}
