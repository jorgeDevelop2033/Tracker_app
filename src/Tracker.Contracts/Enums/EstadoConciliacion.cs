namespace Tracker.Contracts.Enums
{
    /// <summary>
    /// Estado de un tránsito frente al cobro real de la concesionaria.
    /// Por ahora todos nacen <see cref="Pendiente"/>: el cruce contra la cartola
    /// es la fase C. Los demás valores existen para no re-migrar después.
    /// </summary>
    public enum EstadoConciliacion
    {
        /// <summary>Detectado por GPS, aún no contrastado con ningún cobro.</summary>
        Pendiente = 0,
        /// <summary>Cuadra con un cobro de la cartola.</summary>
        Conciliado = 1,
        /// <summary>Lo detectamos pero la concesionaria nunca lo cobró.</summary>
        SinCobro = 2,
        /// <summary>Hay diferencia de monto o de fecha contra el cobro.</summary>
        Discrepancia = 3
    }
}
