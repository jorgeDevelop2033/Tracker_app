#nullable enable
namespace Tracker.Domain.Abstractions
{
    /// <summary>
    /// Fuerza el volcado de escrituras que estén diferidas en memoria.
    ///
    /// <para>
    /// Existe porque el Worker agrupa los <c>GpsFix</c> en lotes antes de
    /// persistirlos. Cualquier operación que necesite leer de la BD fixes que
    /// se acaban de recibir — el cierre de un viaje, que construye la
    /// <c>RutaSimplificada</c> — debe llamar a esto primero, o leerá una traza
    /// incompleta y perderá el último tramo del recorrido para siempre.
    /// </para>
    ///
    /// <para>
    /// La implementación por defecto (<c>SinEscriturasPendientes</c>) no hace
    /// nada: en la API no hay buffer, las escrituras son inmediatas.
    /// </para>
    /// </summary>
    public interface IEscriturasPendientes
    {
        Task VolcarAsync(CancellationToken ct = default);
    }

    /// <summary>No-op para hosts que escriben directo a la BD.</summary>
    public sealed class SinEscriturasPendientes : IEscriturasPendientes
    {
        public Task VolcarAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
