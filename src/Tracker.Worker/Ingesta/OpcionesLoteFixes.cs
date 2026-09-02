#nullable enable
namespace Tracker.Worker.Ingesta
{
    /// <summary>
    /// Parámetros del buffer de escritura por lotes de <c>gps_fix</c>.
    /// </summary>
    public sealed class OpcionesLoteFixes
    {
        public const string SeccionConfig = "IngestaLote";

        /// <summary>
        /// Si es false, cada fix se persiste individualmente (comportamiento
        /// anterior). Útil para depurar sin la indirección del buffer.
        /// </summary>
        public bool Habilitado { get; set; } = true;

        /// <summary>Fixes acumulados que disparan un volcado inmediato.</summary>
        public int TamanoLote { get; set; } = 200;

        /// <summary>
        /// Máximo que un fix espera en memoria antes de volcarse, aunque el lote
        /// no esté lleno. Acota la ventana de pérdida ante una caída del Worker.
        /// </summary>
        public int IntervaloMs { get; set; } = 2_000;

        /// <summary>
        /// Tope del buffer. Si se llena (BD caída o lentísima), los fixes nuevos
        /// se persisten de forma directa en vez de descartarse.
        /// </summary>
        public int CapacidadMaxima { get; set; } = 10_000;
    }
}
