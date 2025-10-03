// ALIAS:
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Tracker;

public sealed class GpsConsumer : BackgroundService
{
    private readonly ILogger<GpsConsumer> _log;
    private readonly IConfiguration _cfg;
    private IConsumer<string, GpsEventV2>? _consumer;

    public GpsConsumer(ILogger<GpsConsumer> log, IConfiguration cfg)
    {
        _log = log;
        _cfg = cfg;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // --- Kafka consumer config ---
        var cCfg = new ConsumerConfig
        {
            BootstrapServers = _cfg["Kafka:BootstrapServers"] ?? "192.168.100.9:9092",
            GroupId = _cfg["Kafka:GroupId"] ?? "tracker.worker.gps",
            EnableAutoCommit = false,                // commit manual
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ClientId = _cfg["Kafka:ClientId"] ?? "tracker.worker",
            BrokerAddressFamily = BrokerAddressFamily.V4
        };

        // --- Schema Registry ---
        var srCfg = new SchemaRegistryConfig
        {
            Url = _cfg["SchemaRegistry:Url"] ?? "http://192.168.100.9:8086"
        };

        var sr = new CachedSchemaRegistryClient(srCfg);

        // Nota: para consumer hay que usar AsSyncOverAsync()
        _consumer = new ConsumerBuilder<string, GpsEventV2>(cCfg)
        .SetKeyDeserializer(Deserializers.Utf8)
            .SetValueDeserializer(new ProtobufDeserializer<GpsEventV2>(sr).AsSyncOverAsync())
            .SetErrorHandler((_, e) => _log.LogError("Kafka error: {Reason} (fatal={Fatal})", e.Reason, e.IsFatal))
            .Build();

        var topic = _cfg["Kafka:Topic"] ?? "tracker.gps.events";
        _consumer.Subscribe(topic);
        _log.LogInformation("✅ Kafka consumer iniciado. Topic='{Topic}', GroupId='{GroupId}'", topic, cCfg.GroupId);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Loop de consumo
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cr = _consumer!.Consume(stoppingToken); // bloqueante hasta que llegue msg o cancelen
                var ev = cr.Message.Value;

                // --- Procesamiento del evento ---
                _log.LogInformation("📍 GPS Device={Device} Lat={Lat} Lon={Lon} Speed={Speed} Heading={Heading} Utc={Utc} Acc={Acc}",
                    ev.DeviceId, ev.Lat, ev.Lon,
                    ev.HasSpeedKph ? ev.SpeedKph : (double?)null,
                    ev.HasHeadingDeg ? ev.HeadingDeg : (double?)null,
                    ev.Utc, ev.HasAccuracyM ? ev.AccuracyM : (double?)null);

                // TODO: llama a tu servicio de negocio aquí
                // await _service.HandleAsync(ev, stoppingToken);

                // Commit manual SOLO cuando el procesamiento terminó OK
                _consumer.Commit(cr);
            }
            catch (ConsumeException ex)
            {
                _log.LogError(ex, "ConsumeException (Topic={Topic}, Partition={Partition}, Offset={Offset})",
                    ex.ConsumerRecord?.Topic, ex.ConsumerRecord?.Partition, ex.ConsumerRecord?.Offset);
                // según el caso podrías pausar partición, DLQ, etc.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // saliendo…
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error procesando mensaje");
                // no hacemos commit -> se reintentará según la política del grupo
                await Task.Delay(250, stoppingToken);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _consumer?.Close(); // leave group, commit final si corresponde
        }
        catch { /* ignore */ }
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        try { _consumer?.Close(); } catch { /* ignore */ }
        _consumer?.Dispose();
        base.Dispose();
    }
}
