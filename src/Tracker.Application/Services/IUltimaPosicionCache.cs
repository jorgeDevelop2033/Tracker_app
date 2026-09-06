using System.Collections.Concurrent;

namespace Tracker.Application.Services
{
    /// <summary>Último fix GPS conocido de un device.</summary>
    public sealed record UltimaPosicion(double Lat, double Lon, DateTime Utc);

    /// <summary>
    /// Guarda el fix anterior de cada device para poder detectar pórticos por el
    /// TRAMO recorrido entre dos muestras y no por proximidad a un punto suelto.
    /// A 100 km/h el teléfono avanza ~55 m entre fixes, así que un radio de 50 m
    /// se puede saltar por completo sin que ninguna muestra caiga dentro.
    /// </summary>
    public interface IUltimaPosicionCache
    {
        UltimaPosicion? Obtener(string deviceId);
        void Guardar(string deviceId, UltimaPosicion pos);
    }

    /// <summary>
    /// Implementación en memoria del proceso. Es deliberadamente volátil: tras un
    /// reinicio del Worker el primer fix de cada device no tiene tramo y cae al
    /// modo antiguo (radio sobre el punto), que sigue siendo correcto.
    /// </summary>
    public sealed class UltimaPosicionCache : IUltimaPosicionCache
    {
        private readonly ConcurrentDictionary<string, UltimaPosicion> _mapa = new();

        /// Se purgan devices inactivos para que un parque grande no crezca sin techo.
        private static readonly TimeSpan TTL = TimeSpan.FromMinutes(30);
        private DateTime _ultimaPurga = DateTime.UtcNow;

        public UltimaPosicion? Obtener(string deviceId)
            => string.IsNullOrEmpty(deviceId) ? null
             : _mapa.TryGetValue(deviceId, out var p) ? p : null;

        public void Guardar(string deviceId, UltimaPosicion pos)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            _mapa[deviceId] = pos;

            if (DateTime.UtcNow - _ultimaPurga < TTL) return;
            _ultimaPurga = DateTime.UtcNow;

            var corte = DateTime.UtcNow - TTL;
            foreach (var (k, v) in _mapa)
                if (v.Utc < corte) _mapa.TryRemove(k, out _);
        }
    }
}
