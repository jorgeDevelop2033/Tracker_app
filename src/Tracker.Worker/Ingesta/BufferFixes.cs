#nullable enable
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Tracker.Domain.Abstractions;
using Tracker.Domain.Entities;

namespace Tracker.Worker.Ingesta
{
    /// <summary>
    /// Acumula <see cref="GpsFix"/> en memoria y los vuelca a la BD por lotes.
    ///
    /// <para>
    /// Antes cada mensaje de Kafka abría su propia transacción para insertar una
    /// sola fila. A 1.000 vehículos son ~35 inserciones por segundo de media (con
    /// picos bastante mayores en hora punta), cada una con su ida y vuelta a SQL
    /// Server. Agrupándolas, el mismo volumen se resuelve con una fracción de las
    /// transacciones y de los round-trips.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Sólo se difiere la <b>persistencia del fix</b>. La detección de pórticos
    /// y el broadcast en vivo siguen ejecutándose de inmediato en <c>GpsConsumer</c>:
    /// no se puede retrasar la alerta de un peaje esperando a que se llene un lote.
    /// </para>
    ///
    /// <para>
    /// Contrapartida asumida: si el Worker muere con el buffer lleno se pierden
    /// hasta <see cref="OpcionesLoteFixes.IntervaloMs"/> de fixes. Es aceptable
    /// porque el fix crudo es un dato operativo y efímero — lo que factura es el
    /// <c>Transito</c>, que sí se persiste de inmediato en la detección.
    /// </para>
    /// </summary>
    public sealed class BufferFixes : IEscriturasPendientes
    {
        private readonly ConcurrentQueue<GpsFix> _cola = new();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly OpcionesLoteFixes _opt;
        private readonly ILogger<BufferFixes> _log;

        // Serializa los volcados: el temporizador y el disparo por lote lleno
        // pueden coincidir, y dos SaveChanges simultáneos sobre el mismo lote
        // insertarían duplicados.
        private readonly SemaphoreSlim _volcando = new(1, 1);

        private int _conteo;

        public BufferFixes(
            IServiceScopeFactory scopeFactory,
            IOptions<OpcionesLoteFixes> opciones,
            ILogger<BufferFixes> log)
        {
            _scopeFactory = scopeFactory;
            _opt = opciones.Value;
            _log = log;
        }

        public int Pendientes => Volatile.Read(ref _conteo);

        /// <summary>
        /// Encola un fix. Si el buffer está saturado lo escribe directo, para no
        /// perder datos cuando la BD va lenta.
        /// </summary>
        public async Task EncolarAsync(GpsFix fix, CancellationToken ct)
        {
            if (!_opt.Habilitado)
            {
                await PersistirDirectoAsync(fix, ct);
                return;
            }

            if (Volatile.Read(ref _conteo) >= _opt.CapacidadMaxima)
            {
                _log.LogWarning(
                    "Buffer de fixes saturado ({Pendientes}). Persistiendo directo — revisar salud de la BD.",
                    Pendientes);

                await PersistirDirectoAsync(fix, ct);
                return;
            }

            _cola.Enqueue(fix);

            if (Interlocked.Increment(ref _conteo) >= _opt.TamanoLote)
                await VolcarLoteAsync(ct);
        }

        /// <summary>
        /// Vuelca un lote de hasta <see cref="OpcionesLoteFixes.TamanoLote"/> fixes.
        /// Lo llama el temporizador de <see cref="VolcadoFixesService"/> y el propio
        /// encolado al llenarse el lote.
        /// </summary>
        public async Task VolcarLoteAsync(CancellationToken ct)
        {
            if (_cola.IsEmpty) return;

            await _volcando.WaitAsync(ct);
            try
            {
                var lote = new List<GpsFix>(_opt.TamanoLote);
                while (lote.Count < _opt.TamanoLote && _cola.TryDequeue(out var fix))
                {
                    Interlocked.Decrement(ref _conteo);
                    lote.Add(fix);
                }

                if (lote.Count == 0) return;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IGpsFixRepository>();
                    await repo.AddRangeAsync(lote, ct);

                    _log.LogDebug("Lote de {Filas} fixes persistido ({Pendientes} en cola).",
                        lote.Count, Pendientes);
                }
                catch (Exception ex)
                {
                    // El lote se pierde a propósito: reencolarlo ante una BD caída
                    // haría crecer el buffer sin techo hasta tumbar al Worker. La
                    // idempotencia por offset de Kafka permite recuperarlos si se
                    // reprocesa la partición.
                    _log.LogError(ex, "Falló el volcado de {Filas} fixes. Lote descartado.", lote.Count);
                }
            }
            finally
            {
                _volcando.Release();
            }
        }

        /// <summary>
        /// Implementación de <see cref="IEscriturasPendientes"/>: vacía el buffer
        /// completo para que un lector vea todos los fixes ya recibidos. Lo usa el
        /// cierre de viaje antes de construir la ruta simplificada.
        /// </summary>
        public Task VolcarAsync(CancellationToken ct = default) => VolcarTodoAsync(ct);

        /// <summary>Vacía el buffer completo. Se usa al apagar el Worker.</summary>
        public async Task VolcarTodoAsync(CancellationToken ct)
        {
            while (!_cola.IsEmpty)
                await VolcarLoteAsync(ct);
        }

        private async Task PersistirDirectoAsync(GpsFix fix, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGpsFixRepository>();
            await repo.AddAsync(fix, ct);
        }
    }
}
