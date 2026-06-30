using Tracker.Contracts.Enums;

namespace Tracker.API.Contracts;

/// <summary>
/// Fila de carga de tarifa para un pórtico (identificado por su Código).
/// ValorPorKm o ValorFijo: al menos uno. Si viene ValorPorKm se multiplica por
/// KmTramo (o la longitud del pórtico) al calcular el tránsito.
/// </summary>
public sealed record TarifaBulkRow(
    string Codigo,
    VehicleCategory Categoria,
    Banda Banda,
    decimal? ValorPorKm,
    decimal? ValorFijo,
    decimal? KmTramo,
    DateTime? VigenteDesde,
    string? Autopista = null);  // si viene, solo aplica a pórticos de esa autopista (códigos colisionan entre concesiones)

/// <summary>
/// Ventana horaria de banda para un pórtico (identificado por su Código).
/// HoraInicio/HoraFin en formato "HH:mm" hora local Chile.
/// </summary>
public sealed record BandaHorarioBulkRow(
    string Codigo,
    DiaTipo DiaTipo,
    string HoraInicio,
    string HoraFin,
    Banda Banda,
    string? Autopista = null);  // si viene, solo aplica a pórticos de esa autopista
