#nullable enable
namespace Tracker.Application.Services
{
    /// <summary>
    /// Umbrales del trayecto recorrido entre dos fixes consecutivos.
    /// </summary>
    public sealed class OpcionesSegmento
    {
        public const string SeccionConfig = "Segmento";

        /// <summary>
        /// Si es false, la detección vuelve a mirar sólo el punto del fix.
        /// </summary>
        public bool Habilitado { get; set; } = true;

        /// <summary>
        /// Antigüedad máxima del fix previo para unirlo con el actual. Pasado esto
        /// no hay forma de saber por dónde fue el vehículo: el segmento sería una
        /// recta que puede cruzar pórticos por los que nunca pasó.
        /// </summary>
        public int MaxSegundosEntreFixes { get; set; } = 30;

        /// <summary>
        /// Longitud máxima del segmento (metros). Segundo cinturón por si el fix
        /// previo es reciente en tiempo pero está lejísimos (salto de multipath).
        /// </summary>
        public double MaxLongitudM { get; set; } = 1_000;

        /// <summary>Entradas antes de forzar una limpieza de devices inactivos.</summary>
        public int MaxDevices { get; set; } = 5_000;
    }

    /// <summary>Última posición conocida de un device, para construir el segmento recorrido.</summary>
    public readonly record struct PosicionPrevia(double Lat, double Lon, DateTime Utc);

    /// <summary>
    /// Guarda el fix anterior de cada device para poder detectar pórticos por el
    /// trayecto recorrido y no sólo por el punto suelto.
    ///
    /// <para>
    /// Vive en memoria del Worker a propósito: es un dato de trabajo, efímero, que
    /// se consulta y se reemplaza en cada mensaje de Kafka. Leerlo de la BD
    /// añadiría un SELECT por fix — justo el round-trip que la escritura por lotes
    /// acaba de quitar. Si el Worker se reinicia, el primer fix de cada device no
    /// tiene segmento y se cae al modo punto; se recupera solo en el siguiente.
    /// </para>
    /// </summary>
    public interface IUltimaPosicion
    {
        /// <summary>Devuelve la posición previa del device, si sigue siendo utilizable.</summary>
        PosicionPrevia? Obtener(string deviceId);

        /// <summary>Registra la posición actual como previa para el próximo fix.</summary>
        void Registrar(string deviceId, double lat, double lon, DateTime utc);
    }

    /// <summary>
    /// No recuerda nada: la detección se cae al modo punto. Es lo que usa la API,
    /// que resuelve el detector por DI pero no ingiere fixes — sólo el Worker
    /// procesa el flujo de Kafka y tiene un "fix anterior" que ofrecer.
    /// </summary>
    public sealed class SinUltimaPosicion : IUltimaPosicion
    {
        public PosicionPrevia? Obtener(string deviceId) => null;
        public void Registrar(string deviceId, double lat, double lon, DateTime utc) { }
    }
}
