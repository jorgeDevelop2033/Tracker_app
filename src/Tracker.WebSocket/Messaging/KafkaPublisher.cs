using Confluent.Kafka;
using Confluent.SchemaRegistry.Serdes;
using Confluent.SchemaRegistry;
using Google.Protobuf;
using System.Collections.Concurrent;

namespace Tracker.WebSocket.Messaging
{
    public sealed class KafkaPublisher : IKafkaPublisher
    {
        private readonly ILogger<KafkaPublisher> _log;
        private readonly IConfiguration _cfg;
        private ISchemaRegistryClient? _sr;
        private readonly ConcurrentDictionary<Type, object> _producers = new();

        public KafkaPublisher(IConfiguration cfg, ILogger<KafkaPublisher> log)
        {
            _cfg = cfg; _log = log;
        }

        private ProducerConfig ProducerCfg() => new()
        {
            BootstrapServers = _cfg["Kafka:BootstrapServers"],
            ClientId = _cfg["Kafka:ClientId"] ?? "tracker.websocket",
            Acks = Acks.All,
            EnableIdempotence = true,
            CompressionType = CompressionType.Zstd,
            LingerMs = 10,
            BatchSize = 128 * 1024,
            MessageTimeoutMs = 30000,
            BrokerAddressFamily = BrokerAddressFamily.V4
        };

        private ISchemaRegistryClient SR() =>
            _sr ??= new CachedSchemaRegistryClient(new SchemaRegistryConfig
            {
                Url = _cfg["SchemaRegistry:Url"]
            });

        private IProducer<string, T> GetProducer<T>() where T : class, IMessage<T>, new()
        {
            var t = typeof(T);
            if (_producers.TryGetValue(t, out var exist)) return (IProducer<string, T>)exist;

            var subjectStrategy = (_cfg["SchemaRegistry:SubjectNameStrategy"] ?? "TopicRecord") switch
            {
                "Topic" => SubjectNameStrategy.Topic,
                "Record" => SubjectNameStrategy.Record,
                _ => SubjectNameStrategy.TopicRecord
            };

            var p = new ProducerBuilder<string, T>(ProducerCfg())
                .SetKeySerializer(Serializers.Utf8)
                .SetValueSerializer(new ProtobufSerializer<T>(SR(), new ProtobufSerializerConfig
                {
                    SubjectNameStrategy = subjectStrategy
                }))
                .SetErrorHandler((_, e) => _log.LogError("Kafka error: {Reason} (fatal={Fatal})", e.Reason, e.IsFatal))
                .Build();

            _producers[t] = p;
            _log.LogInformation("Producer Protobuf creado para {Type}", t.Name);
            return p;
        }

        public async Task PublishAsync<T>(string topic, string key, T message) where T : class, IMessage<T>, new()
        {
            var prod = GetProducer<T>();
            var dr = await prod.ProduceAsync(topic, new Message<string, T>
            {
                Key = key,
                Value = message,
                Timestamp = new Timestamp(DateTime.UtcNow)
            });
            // opcional log: _log.LogInformation("→ {Topic}[{P}]@{O}", dr.Topic, dr.Partition, dr.Offset);
        }

        public void Dispose()
        {
            foreach (var p in _producers.Values) { try { ((IDisposable)p).Dispose(); } catch { } }
            _sr?.Dispose();
        }
    }
}
