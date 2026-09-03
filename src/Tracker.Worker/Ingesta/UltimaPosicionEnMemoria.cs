#nullable enable
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Tracker.Application.Services;

namespace Tracker.Worker.Ingesta
{
    /// <summary>
    /// Cache en memoria de la última posición por device.
    /// </summary>
    public sealed class UltimaPosicionEnMemoria : IUltimaPosicion
    {
        private readonly ConcurrentDictionary<string, PosicionPrevia> _posiciones = new();
        private readonly OpcionesSegmento _opt;
        private readonly ILogger<UltimaPosicionEnMemoria> _log;

        public UltimaPosicionEnMemoria(IOptions<OpcionesSegmento> opciones, ILogger<UltimaPosicionEnMemoria> log)
        {
            _opt = opciones.Value;
            _log = log;
        }

        public PosicionPrevia? Obtener(string deviceId)
        {
            if (!_opt.Habilitado || string.IsNullOrEmpty(deviceId))
                return null;

            if (!_posiciones.TryGetValue(deviceId, out var previa))
                return null;

            return previa;
        }

        public void Registrar(string deviceId, double lat, double lon, DateTime utc)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            // Un fix atrasado (reenvío tras recuperar señal) no debe pisar una
            // posición más nueva: el segmento del próximo fix se construiría hacia
            // atrás en el tiempo.
            _posiciones.AddOrUpdate(
                deviceId,
                new PosicionPrevia(lat, lon, utc),
                (_, actual) => utc > actual.Utc ? new PosicionPrevia(lat, lon, utc) : actual);

            if (_posiciones.Count > _opt.MaxDevices)
                Limpiar(utc);
        }

        /// <summary>
        /// Descarta devices que llevan tiempo sin emitir. Sin esto el diccionario
        /// crece con cada device que pasa por el sistema y nunca libera memoria,
        /// en un contenedor que corre con mem_limit de 256 MB.
        /// </summary>
        private void Limpiar(DateTime ahoraUtc)
        {
            var corte = ahoraUtc.AddMinutes(-15);
            var eliminados = 0;

            foreach (var (device, pos) in _posiciones)
            {
                if (pos.Utc < corte && _posiciones.TryRemove(device, out _))
                    eliminados++;
            }

            if (eliminados > 0)
                _log.LogDebug("Cache de posiciones: {Eliminados} devices inactivos descartados.", eliminados);
        }
    }
}
