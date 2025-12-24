using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AuditSync.OracleConsumer.Infrastructure.Kafka;

/// <summary>
/// Service for consuming messages from Kafka with manual offset management.
/// </summary>
public class KafkaConsumerService : IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaConsumerService> _logger;
    private bool _disposed = false;

    public KafkaConsumerService(IConsumer<string, string> consumer, ILogger<KafkaConsumerService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    /// <summary>
    /// Consumes a message from Kafka.
    /// </summary>
    public ConsumeResult<string, string> Consume(CancellationToken cancellationToken)
    {
        return _consumer.Consume(cancellationToken);
    }

    /// <summary>
    /// Manually commits the offset after successful processing.
    /// This ensures at-least-once delivery semantics.
    /// </summary>
    public void Commit(ConsumeResult<string, string> result)
    {
        _consumer.Commit(result);
        _logger.LogDebug("Committed offset {Offset} for partition {Partition}",
            result.Offset, result.Partition);
    }

    public void Subscribe(string topic)
    {
        _consumer.Subscribe(topic);
        _logger.LogInformation("Subscribed to Kafka topic: {Topic}", topic);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _consumer?.Close();
            _consumer?.Dispose();
            _logger.LogInformation("Kafka consumer closed and disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Kafka consumer disposal");
        }
        finally
        {
            _disposed = true;
        }
    }
}
