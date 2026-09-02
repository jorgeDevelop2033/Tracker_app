#nullable enable
using Microsoft.Extensions.Options;

namespace Tracker.Worker.Ingesta
{
    /// <summary>
    /// Vuelca el <see cref="BufferFixes"/> cada
    /// <see cref="OpcionesLoteFixes.IntervaloMs"/>, de modo que un fix nunca se
    /// quede en memoria esperando a que se llene el lote. Con poco tráfico (un
    /// solo vehículo circulando) el lote tardaría minutos en completarse y la
    /// posición no llegaría nunca a la BD.
    ///
    /// <para>
    /// Al apagar hace un volcado final: es lo que evita perder lo pendiente en un
    /// redeploy, que es cuando más veces se reinicia el Worker.
    /// </para>
    /// </summary>
    public sealed class VolcadoFixesService : BackgroundService
    {
        private readonly BufferFixes _buffer;
        private readonly OpcionesLoteFixes _opt;
        private readonly ILogger<VolcadoFixesService> _log;

        public VolcadoFixesService(
            BufferFixes buffer,
            IOptions<OpcionesLoteFixes> opciones,
            ILogger<VolcadoFixesService> log)
        {
            _buffer = buffer;
            _opt = opciones.Value;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opt.Habilitado)
            {
                _log.LogInformation("Escritura por lotes deshabilitada: cada fix se persiste directo.");
                return;
            }

            _log.LogInformation(
                "Volcado de fixes activo: lote={Lote}, intervalo={Intervalo}ms, capacidad={Capacidad}.",
                _opt.TamanoLote, _opt.IntervaloMs, _opt.CapacidadMaxima);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_opt.IntervaloMs));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await _buffer.VolcarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // apagando
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            var pendientes = _buffer.Pendientes;
            if (pendientes > 0)
                _log.LogInformation("Volcando {Pendientes} fixes pendientes antes de apagar.", pendientes);

            // CancellationToken.None a propósito: si se propaga el token de apagado,
            // el volcado final se cancela justo cuando más importa completarlo.
            try
            {
                await _buffer.VolcarTodoAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falló el volcado final de fixes.");
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
