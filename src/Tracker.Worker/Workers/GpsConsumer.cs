// ALIAS:
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Tracker;

using Microsoft.Extensions.DependencyInjection;          // 👈 para IServiceScopeFactory
using Tracker.Worker.Application.Services;               // IGpsIngestService
using Tracker.Worker.Application.Dtos;                   // GpsEventDto, KafkaMetaDto

public sealed class GpsConsumer : BackgroundService
{
    private readonly ILogger<GpsConsumer> _log;
    private readonly IConfiguration _cfg;
    private readonly IServiceScopeFactory _scopeFactory; // 👈 en vez de IGpsIngestService
    private IConsumer<string, GpsEventV2>? _consumer;

    private DateTime _lastHeartbeat = DateTime.MinValue;

    public GpsConsumer(ILogger<GpsConsumer> log, IConfiguration cfg, IServiceScopeFactory scopeFactory)
    {
        _log = log;
        _cfg = cfg;
        _scopeFactory = scopeFactory;                    // 👈 guardamos el scope factory
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine(@"
==========================================
   🚀 Iniciando Tracker.Worker (GpsConsumer)
==========================================
");

        var cCfg = new ConsumerConfig
        {
            BootstrapServers = _cfg["Kafka:BootstrapServers"] ?? "45.7.228.18:9092",
            GroupId = _cfg["Kafka:GroupId"] ?? "tracker.worker.gps",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ClientId = _cfg["Kafka:ClientId"] ?? "tracker.worker",
            BrokerAddressFamily = BrokerAddressFamily.V4,
            StatisticsIntervalMs = 30000,
            EnablePartitionEof = true,
        };

        var srCfg = new SchemaRegistryConfig
        {
            Url = _cfg["SchemaRegistry:Url"] ?? "http://45.7.228.18:8086"
        };

        var sr = new CachedSchemaRegistryClient(srCfg);

        _consumer = new ConsumerBuilder<string, GpsEventV2>(cCfg)
            .SetKeyDeserializer(Deserializers.Utf8)
            .SetValueDeserializer(new ProtobufDeserializer<GpsEventV2>(sr).AsSyncOverAsync())
            .SetErrorHandler((_, e) => _log.LogError("Kafka error: {Reason} (fatal={Fatal})", e.Reason, e.IsFatal))
            .SetPartitionsAssignedHandler((c, parts) =>
            {
                _log.LogInformation("📦 Particiones asignadas: {Parts}",
                    string.Join(", ", parts.Select(p => $"{p.Topic}[{p.Partition}]")));
                return parts.Select(p => new TopicPartitionOffset(p, Offset.Stored)); // 👈 NO Assign aquí
            })
            .SetPartitionsRevokedHandler((c, parts) =>
            {
                _log.LogWarning("♻️ Particiones revocadas: {Parts}",
                    string.Join(", ", parts.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .SetPartitionsLostHandler((c, parts) =>
            {
                _log.LogError("💥 Particiones perdidas: {Parts}",
                    string.Join(", ", parts.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .Build();

        var topic = _cfg["Kafka:Topic"] ?? "tracker.gps.events";
        _consumer.Subscribe(topic);

        _log.LogInformation("✅ Kafka consumer iniciado. Topic='{Topic}', GroupId='{GroupId}', Brokers='{Brokers}'",
            topic, cCfg.GroupId, cCfg.BootstrapServers);

        Console.WriteLine($"✅ Suscrito a topic: {topic} | GroupId: {cCfg.GroupId} | Brokers: {cCfg.BootstrapServers}");
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cr = _consumer!.Consume(TimeSpan.FromSeconds(1));
                if (cr is null)
                {
                    if ((DateTime.UtcNow - _lastHeartbeat).TotalSeconds >= 30)
                    {
                        _lastHeartbeat = DateTime.UtcNow;
                        _log.LogInformation("⏳ Esperando mensajes… {NowUtc}", _lastHeartbeat);
                    }
                    continue;
                }
                if (cr.IsPartitionEOF) continue;

                var ev = cr.Message.Value;
                if (ev is null)
                {
                    _log.LogWarning("Mensaje nulo en {Topic}[{Partition}]@{Offset}", cr.Topic, cr.Partition.Value, cr.Offset.Value);
                    SafeCommit(cr);
                    continue;
                }

                var dto = new GpsEventDto(
                    DeviceId: ev.DeviceId,
                    Lat: ev.Lat,
                    Lon: ev.Lon,
                    SpeedKph: ev.HasSpeedKph ? ev.SpeedKph : null,
                    HeadingDeg: ev.HasHeadingDeg ? ev.HeadingDeg : null,
                    Utc: ev.Utc.ToDateTime(),
                    AccuracyM: ev.HasAccuracyM ? ev.AccuracyM : null
                );

                var meta = new KafkaMetaDto(
                    Topic: cr.Topic,
                    Partition: cr.Partition.Value,
                    Offset: cr.Offset.Value,
                    Key: cr.Message.Key
                );

                // 👇 CREA SCOPE y resuelve el servicio scoped aquí
                using (var scope = _scopeFactory.CreateScope())
                {
                    var ingest = scope.ServiceProvider.GetRequiredService<IGpsIngestService>();
                    await ingest.IngestAsync(dto, meta, stoppingToken);
                }

                _log.LogInformation("📍 Guardado Device={Device} Lat={Lat} Lon={Lon} @ {Utc} (offset {Partition}:{Offset})",
                    dto.DeviceId, dto.Lat, dto.Lon, dto.Utc, meta.Partition, meta.Offset);

                SafeCommit(cr);
            }
            catch (ConsumeException ex)
            {
                _log.LogError(ex, "ConsumeException (Topic={Topic}, Partition={Partition}, Offset={Offset})",
                    ex.ConsumerRecord?.Topic, ex.ConsumerRecord?.Partition, ex.ConsumerRecord?.Offset);
                await Task.Delay(250, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // saliendo…
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error procesando mensaje");
                await Task.Delay(250, stoppingToken);
            }
        }
    }

    private void SafeCommit(ConsumeResult<string, GpsEventV2> cr)
    {
        try { _consumer!.Commit(cr); }
        catch (KafkaException kex)
        {
            _log.LogWarning(kex, "Commit falló para {Partition}:{Offset}", cr.Partition.Value, cr.Offset.Value);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try { _consumer?.Close(); } catch { /* ignore */ }
        Console.WriteLine("🛑 GpsConsumer detenido.");
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        try { _consumer?.Close(); } catch { /* ignore */ }
        _consumer?.Dispose();
        base.Dispose();
    }
}
