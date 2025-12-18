using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Infrastructure.Kafka;
using System.Text.Json;

namespace AuditSync.OracleConsumer.App.Services;

/// <summary>
/// Background service that continuously consumes audit messages from Kafka.
/// </summary>
public class AuditConsumerBackgroundService : BackgroundService
{
    private readonly KafkaConsumerService _kafkaConsumer;
    private readonly IRuleEngine _ruleEngine;
    private readonly IAuditDataService _auditDataService;
    private readonly ILogger<AuditConsumerBackgroundService> _logger;
    private readonly string _topic;

    public AuditConsumerBackgroundService(
        KafkaConsumerService kafkaConsumer,
        IRuleEngine ruleEngine,
        IAuditDataService auditDataService,
        IConfiguration configuration,
        ILogger<AuditConsumerBackgroundService> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _ruleEngine = ruleEngine;
        _auditDataService = auditDataService;
        _logger = logger;
        _topic = configuration["KAFKA_TOPIC"] ?? "oracle.audit.events";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuditSync Consumer starting...");

        // Subscribe to Kafka topic
        _kafkaConsumer.Subscribe(_topic);

        _logger.LogInformation("Subscribed to topic: {Topic}", _topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Consume message from Kafka
                var consumeResult = _kafkaConsumer.Consume(stoppingToken);

                if (consumeResult?.Message?.Value == null)
                {
                    continue;
                }

                _logger.LogDebug("Consumed message from partition {Partition}, offset {Offset}",
                    consumeResult.Partition.Value, consumeResult.Offset.Value);

                // Deserialize JSON message
                var auditMessage = JsonSerializer.Deserialize<AuditMessage>(
                    consumeResult.Message.Value,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (auditMessage == null)
                {
                    _logger.LogWarning("Failed to deserialize message at offset {Offset}", consumeResult.Offset.Value);
                    continue;
                }

                // Apply extraction rules
                var extractedData = await _ruleEngine.ApplyRulesAsync(auditMessage);

                // Save to database (transactional)
                await _auditDataService.SaveAuditDataAsync(
                    auditMessage,
                    extractedData,
                    consumeResult.Partition.Value,
                    consumeResult.Offset.Value);

                // Commit offset only after successful database write
                _kafkaConsumer.Commit(consumeResult);

                _logger.LogInformation(
                    "Successfully processed message {MessageId} (offset: {Offset})",
                    auditMessage.Id,
                    consumeResult.Offset.Value);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message - will retry from last committed offset");
                // Don't commit offset on error - Kafka will redeliver the message
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("AuditSync Consumer stopped");
    }

    public override void Dispose()
    {
        _kafkaConsumer?.Dispose();
        base.Dispose();
    }
}
