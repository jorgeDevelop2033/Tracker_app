using System;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tracker.WebSocket.Messaging
{   public interface IKafkaPublisher : IDisposable
    {
        Task PublishAsync<T>(string topic, string key, T message) where T : class, IMessage<T>, new();
    }
}
