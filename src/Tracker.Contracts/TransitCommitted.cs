using System;
using Tracker.Contracts.Enums;

namespace Tracker.Contracts;

/// <summary>
/// Tránsito confirmado y persistido, con cálculo tarifario aplicado.
/// </summary>
public sealed record TransitCommitted(
    Guid TransitoId,
    Guid PorticoId,
    string PorticoCodigo,
    string Autopista,
    Banda Banda,                // TBFP | TBP | TS
    VehicleCategory Categoria,  // C1/C2/C3/C4 (puedes mapear 1&4 a C1 si corresponde)
    decimal Precio,
    DateTime Utc
);
