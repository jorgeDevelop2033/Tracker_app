#nullable enable
using Tracker.Application.Services;
using Tracker.Contracts.Enums;
using Tracker.Domain.Viajes;

namespace Tracker.Worker.Workers
{
    /// <summary>
    /// Cierra los viajes que quedaron abiertos sin que nadie los detuviera.
    /// <para>
    /// Es la red de seguridad del modo explícito: la app puede morir, quedarse
    /// sin batería o perder señal en mitad del viaje, y un viaje eterno
    /// distorsiona todos los totales del vehículo (y captura tránsitos de días
    /// siguientes que no le corresponden).
    /// </para>
    /// </summary>
    public sealed class CierreViajesService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _cfg;
        private readonly ILogger<CierreViajesService> _log;

        public CierreViajesService(
            IServiceScopeFactory scopeFactory,
            IConfiguration cfg,
            ILogger<CierreViajesService> log)
        {
            _scopeFactory = scopeFactory;
            _cfg = cfg;
            _log = log;
        }

        /// <summary>Minutos sin GPS tras los cuales se da el viaje por terminado.</summary>
        private int UmbralMinutos =>
            int.TryParse(_cfg["Viajes:InactividadMinutos"], out var m) && m > 0 ? m : 30;

        /// <summary>Cada cuánto se revisa. No tiene sentido más seguido que el umbral.</summary>
        private TimeSpan Intervalo =>
            TimeSpan.FromMinutes(
                int.TryParse(_cfg["Viajes:IntervaloRevisionMinutos"], out var m) && m > 0 ? m : 5);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation(
                "🧹 Cierre de viajes activo: umbral {Umbral} min, revisión cada {Intervalo}.",
                UmbralMinutos, Intervalo);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RevisarAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // apagado normal
                }
                catch (Exception ex)
                {
                    // Nunca dejar morir el loop: si la BD está caída un rato,
                    // reintentamos en el siguiente ciclo.
                    _log.LogError(ex, "Error revisando viajes para cierre automático.");
                }

                try
                {
                    await Task.Delay(Intervalo, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RevisarAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var viajes = scope.ServiceProvider.GetRequiredService<IViajeRepository>();
            var servicio = scope.ServiceProvider.GetRequiredService<IViajeService>();

            var umbral = DateTime.UtcNow.AddMinutes(-UmbralMinutos);
            var candidatos = await viajes.ListCandidatosCierreAsync(umbral, ct);

            if (candidatos.Count == 0) return;

            _log.LogInformation("Cerrando {Cantidad} viaje(s) sin actividad desde {Umbral:u}.",
                candidatos.Count, umbral);

            foreach (var viaje in candidatos)
            {
                if (ct.IsCancellationRequested) break;

                // El servicio ajusta FinUtc al último fix del viaje; el umbral es
                // solo el respaldo para viajes que nunca recibieron GPS.
                await servicio.FinalizarAsync(viaje.Id, umbral, EstadoViaje.CerradoPorInactividad, ct);
            }
        }
    }
}
