namespace Tracker.Contracts.Enums
{
    /// <summary>
    /// Ciclo de vida de un viaje. El conductor lo abre y lo cierra desde la app;
    /// si la app muere o se queda sin señal, el Worker lo cierra por inactividad
    /// para que no quede contaminando los totales.
    /// </summary>
    public enum EstadoViaje
    {
        EnCurso = 0,
        Finalizado = 1,
        /// <summary>Cerrado por el job de inactividad, no por el conductor.</summary>
        CerradoPorInactividad = 2
    }
}
