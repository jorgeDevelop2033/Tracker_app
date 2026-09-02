#nullable enable
using Tracker.Application.Dtos;

namespace Tracker.Application.Services
{
    /// <summary>
    /// Punto de una traza ya filtrada para dibujo.
    /// </summary>
    public readonly record struct PuntoTraza(
        double Lat,
        double Lon,
        double? SpeedKph,
        double? HeadingDeg,
        double? AccuracyM,
        DateTime Utc);

    /// <summary>
    /// Umbrales del filtro de calidad de traza. Valores por defecto pensados
    /// para conducción urbana en Santiago.
    /// </summary>
    public sealed class OpcionesCalidadTraza
    {
        public const string SeccionConfig = "Traza";

        /// <summary>
        /// Precisión peor que esto (metros) se descarta. En ciudad con edificios
        /// altos el multipath produce fixes de 30-50 m que caen sobre la manzana.
        /// </summary>
        public double MaxAccuracyM { get; set; } = 30;

        /// <summary>
        /// Velocidad implícita máxima entre dos fixes (km/h). Por encima de esto
        /// el salto no es movimiento real sino rebote de señal.
        /// </summary>
        public double MaxVelocidadImplicitaKph { get; set; } = 200;

        /// <summary>
        /// Distancia (metros) por debajo de la cual dos fixes se consideran el
        /// mismo punto. Evita el "ovillo" del vehículo detenido en un semáforo.
        /// </summary>
        public double MinDistanciaM { get; set; } = 5;

        /// <summary>Si es false, el filtro devuelve la traza tal cual llega.</summary>
        public bool Habilitado { get; set; } = true;
    }

    /// <summary>
    /// Limpia una traza GPS para dibujarla en el mapa.
    ///
    /// <para>
    /// Existe porque la traza cruda se ve mal justo donde más se mira: al doblar
    /// una esquina. Ahí se juntan tres cosas — los edificios degradan la precisión
    /// (multipath), los fixes llegan por un pipeline de varios saltos y pueden
    /// desordenarse, y el vehículo detenido genera decenas de fixes en el mismo
    /// punto. El resultado es una línea que zigzaguea y "atraviesa" manzanas.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Esto es <b>exclusivamente para presentación</b>. No debe usarse nunca en
    /// la ingesta ni antes de la detección de pórticos: un fix descartado por
    /// precisión mala puede ser justo el que caía dentro del radio de 50 m de un
    /// pórtico, y perderlo significa un tránsito no cobrado. Se filtra lo que se
    /// dibuja, no lo que se guarda.
    /// </para>
    /// </summary>
    public static class FiltroCalidadTraza
    {
        private const double RadioTierraM = 6_371_000;

        /// <summary>
        /// Aplica el filtro completo: ordena por timestamp, descarta baja
        /// precisión, saltos imposibles y puntos redundantes.
        /// </summary>
        public static List<PuntoTraza> Filtrar(
            IEnumerable<PuntoTraza> puntos,
            OpcionesCalidadTraza? opciones = null)
        {
            var opt = opciones ?? new OpcionesCalidadTraza();

            // Orden por timestamp del dispositivo, no por orden de llegada.
            // El fix viaja móvil -> SignalR -> Kafka -> Worker -> HTTP -> SignalR,
            // así que un mensaje retrasado puede llegar después de uno más nuevo.
            // Pintado en orden de llegada, eso dibuja un pico hacia atrás y vuelta.
            var ordenados = puntos.OrderBy(p => p.Utc).ToList();

            if (!opt.Habilitado || ordenados.Count == 0)
                return ordenados;

            var resultado = new List<PuntoTraza>(ordenados.Count);

            foreach (var actual in ordenados)
            {
                // Precisión reportada por el GPS del móvil. Null = desconocida:
                // se acepta, porque no todos los dispositivos la informan.
                if (actual.AccuracyM is > 0 && actual.AccuracyM > opt.MaxAccuracyM)
                    continue;

                if (resultado.Count == 0)
                {
                    resultado.Add(actual);
                    continue;
                }

                var previo = resultado[^1];
                var metros = DistanciaM(previo.Lat, previo.Lon, actual.Lat, actual.Lon);

                // Punto redundante: el vehículo no se movió lo suficiente como para
                // aportar forma al trazo. Se descarta para no acumular ruido.
                if (metros < opt.MinDistanciaM)
                    continue;

                var segundos = (actual.Utc - previo.Utc).TotalSeconds;
                if (segundos > 0)
                {
                    var kph = metros / segundos * 3.6;

                    // Salto físicamente imposible: es multipath, no movimiento.
                    if (kph > opt.MaxVelocidadImplicitaKph)
                        continue;
                }

                resultado.Add(actual);
            }

            return resultado;
        }

        /// <summary>Distancia haversine en metros entre dos coordenadas.</summary>
        public static double DistanciaM(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = GradosARadianes(lat2 - lat1);
            var dLon = GradosARadianes(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(GradosARadianes(lat1)) * Math.Cos(GradosARadianes(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return RadioTierraM * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double GradosARadianes(double grados) => grados * Math.PI / 180;
    }
}
