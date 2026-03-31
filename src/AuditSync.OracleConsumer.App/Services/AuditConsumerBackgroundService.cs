using AuditSync.OracleConsumer.Application.Services;
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
    private readonly IAuditMessageRepository _auditMessageRepository;
    private readonly IRuleRepository _ruleRepository;
    private readonly ITargetRepository _targetRepository;
    private readonly IExtractionService _extractionService;
    private readonly ICaseService _caseService;
    private readonly ILogger<AuditConsumerBackgroundService> _logger;
    private readonly string _topic;

    // NEW: Rules engine components
    private readonly RulesCache? _rulesCache;
    private readonly RulesEngineService? _rulesEngineService;
    private readonly IRuleExtractionRepository? _ruleExtractionRepository;
    private readonly ICaseRepository? _caseRepository;
    private readonly bool _useRulesEngine;

    public AuditConsumerBackgroundService(
        KafkaConsumerService kafkaConsumer,
        IAuditMessageRepository auditMessageRepository,
        IRuleRepository ruleRepository,
        ITargetRepository targetRepository,
        IExtractionService extractionService,
        ICaseService caseService,
        IConfiguration configuration,
        ILogger<AuditConsumerBackgroundService> logger,
        RulesCache? rulesCache = null,
        RulesEngineService? rulesEngineService = null,
        IRuleExtractionRepository? ruleExtractionRepository = null,
        ICaseRepository? caseRepository = null)
    {
        _kafkaConsumer = kafkaConsumer;
        _auditMessageRepository = auditMessageRepository;
        _ruleRepository = ruleRepository;
        _targetRepository = targetRepository;
        _extractionService = extractionService;
        _caseService = caseService;
        _logger = logger;
        _topic = configuration["KAFKA_TOPIC"] ?? "oracle.audit.events";

        // NEW: Optional rules engine (if configured)
        _rulesCache = rulesCache;
        _rulesEngineService = rulesEngineService;
        _ruleExtractionRepository = ruleExtractionRepository;
        _caseRepository = caseRepository;
        _useRulesEngine = rulesCache != null && rulesEngineService != null;

        if (_useRulesEngine)
        {
            _logger.LogInformation("Rules engine is ENABLED");
        }
        else
        {
            _logger.LogInformation("Rules engine is DISABLED - using legacy extraction rules");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuditSync Consumer starting...");
        _logger.LogInformation("Rules engine enabled: {Enabled}", _useRulesEngine);

        // Subscribe to Kafka topic
        _kafkaConsumer.Subscribe(_topic);

        _logger.LogInformation("Subscribed to topic: {Topic}", _topic);
        _logger.LogInformation("Waiting for messages from Kafka...");

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

                _logger.LogDebug("Kafka Message: {auditMessage}", consumeResult.Message.Value);

                if (auditMessage == null)
                {
                    _logger.LogWarning("Failed to deserialize message at offset {Offset}", consumeResult.Offset.Value);
                    continue;
                }

                // Generate ID if not present in the message (composite key: SessionId_EntryId_Statement)
                if (string.IsNullOrEmpty(auditMessage.Id))
                {
                    auditMessage.Id = $"{auditMessage.SessionId}_{auditMessage.EntryId}_{auditMessage.Statement}";
                }

                // Check if message has a target
                if (string.IsNullOrWhiteSpace(auditMessage.Target))
                {
                    _logger.LogDebug("Message {MessageId} has no target, skipping (not storing)", auditMessage.Id);
                    _kafkaConsumer.Commit(consumeResult);
                    continue;
                }

                // Check if target exists in database
                var targetExists = await _targetRepository.ExistsAsync(auditMessage.Target);
                if (!targetExists)
                {
                    _logger.LogDebug("Message {MessageId} has target '{Target}' that does not exist in database, skipping (not storing)",
                        auditMessage.Id, auditMessage.Target);
                    _kafkaConsumer.Commit(consumeResult);
                    continue;
                }

                // NEW: Apply rules engine BEFORE storing audit log (if enabled)
                if (_useRulesEngine)
                {
                    await ProcessWithRulesEngineAsync(auditMessage, consumeResult, stoppingToken);
                }
                else
                {
                    // LEGACY: Use old extraction rules
                    await ProcessWithLegacyRulesAsync(auditMessage, consumeResult);
                }

                // Commit offset only after successful processing
                _kafkaConsumer.Commit(consumeResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer cancelled");
                break;
            }
            catch (ObjectDisposedException)
            {
                // Consumer was disposed during shutdown - this is expected
                _logger.LogInformation("Consumer disposed during shutdown");
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

    /// <summary>
    /// Process message using new rules engine (JavaScript-based).
    /// Flow: Apply Rules → Store audit_logs → Create case (if matched) → Store extractions
    /// </summary>
    private async Task ProcessWithRulesEngineAsync(
        AuditMessage auditMessage,
        Confluent.Kafka.ConsumeResult<string, string> consumeResult,
        CancellationToken stoppingToken)
    {
        // Step 1: Fetch cached rules
        var rules = await _rulesCache!.GetRulesAsync(stoppingToken);
        _logger.LogDebug("Loaded {Count} rules from cache", rules.Count);

        // Step 2: Evaluate rules (short-circuit on first match)
        var matchedRule = await _rulesEngineService!.EvaluateRulesAsync(rules, auditMessage, stoppingToken);

        // Step 3: Store audit log (always store, regardless of rule match)
        await _auditMessageRepository.SaveAsync(
            auditMessage,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value);
        _logger.LogDebug("Stored audit log {AuditLogId}", auditMessage.Id);

        // Step 4: Create case and store extractions if rule matched
        if (matchedRule != null && matchedRule.Matched && matchedRule.Actions.CreateCase)
        {
            // Create case
            var caseEntity = new Case
            {
                Id = $"case-{Guid.NewGuid()}",
                AuditLogId = auditMessage.Id,
                CaseStatus = "OPEN",
                Valid = null, // Consumer never sets VALID field
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var caseId = await _caseRepository!.CreateAsync(caseEntity);

            // Store extractions if any
            if (matchedRule.Extractions != null && matchedRule.Extractions.Count > 0)
            {
                // Populate additional fields for case_extractions table
                foreach (var extraction in matchedRule.Extractions)
                {
                    extraction.AuditLogId = auditMessage.Id;
                    extraction.RuleId = matchedRule.RuleId;
                    extraction.RuleName = matchedRule.RuleName;
                }

                await _ruleExtractionRepository!.InsertExtractionsAsync(
                    caseId,
                    matchedRule.Extractions,
                    stoppingToken);

                _logger.LogInformation(
                    "Rule '{RuleName}' matched - Created case {CaseId} with {ExtractionCount} extraction(s) for message {MessageId} (offset: {Offset})",
                    matchedRule.RuleName,
                    caseId,
                    matchedRule.Extractions.Count,
                    auditMessage.Id,
                    consumeResult.Offset.Value);
            }
            else
            {
                _logger.LogInformation(
                    "Rule '{RuleName}' matched - Created case {CaseId} without extractions for message {MessageId} (offset: {Offset})",
                    matchedRule.RuleName,
                    caseId,
                    auditMessage.Id,
                    consumeResult.Offset.Value);
            }
        }
        else
        {
            _logger.LogDebug(
                "No rules matched for message {MessageId} - Audit log stored, no case created (offset: {Offset})",
                auditMessage.Id,
                consumeResult.Offset.Value);
        }
    }

    /// <summary>
    /// Process message using legacy extraction rules (regex-based).
    /// Flow: Store audit_logs → Apply extraction rules → Create case (if extractions found)
    /// </summary>
    private async Task ProcessWithLegacyRulesAsync(
        AuditMessage auditMessage,
        Confluent.Kafka.ConsumeResult<string, string> consumeResult)
    {
        // Step 1: Store audit log (MERGE operation) - store all messages with a target
        await _auditMessageRepository.SaveAsync(
            auditMessage,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value);

        // Step 2: Load extraction rules for this target
        var rules = await _ruleRepository.GetRulesByTargetAsync(auditMessage.Target!);

        if (rules == null || rules.Count == 0)
        {
            _logger.LogDebug("No extraction rules found for target: {Target}, skipping extraction (audit log stored)",
                auditMessage.Target);
            return;
        }

        // Step 3: Apply extraction rules
        var extractedValues = await _extractionService.ApplyRulesAsync(auditMessage, rules);

        // Step 4: If ANY extraction succeeded, create case with extractions
        if (extractedValues != null && extractedValues.Count > 0)
        {
            var caseId = await _caseService.CreateCaseWithExtractionsAsync(
                auditMessage,
                extractedValues);

            if (caseId != null)
            {
                _logger.LogInformation(
                    "Successfully processed message {MessageId} - Created case {CaseId} with {ExtractionCount} extraction(s) (offset: {Offset})",
                    auditMessage.Id,
                    caseId,
                    extractedValues.Count,
                    consumeResult.Offset.Value);
            }
            else
            {
                _logger.LogInformation(
                    "Successfully processed message {MessageId} - Case already exists (reprocessing) (offset: {Offset})",
                    auditMessage.Id,
                    consumeResult.Offset.Value);
            }
        }
        else
        {
            _logger.LogDebug(
                "Successfully processed message {MessageId} - No extractions, no case created (offset: {Offset})",
                auditMessage.Id,
                consumeResult.Offset.Value);
        }
    }

    public override void Dispose()
    {
        _kafkaConsumer?.Dispose();
        base.Dispose();
    }
}
