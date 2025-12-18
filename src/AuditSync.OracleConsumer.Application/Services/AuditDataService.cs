using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Service for transactional persistence of audit data.
/// Coordinates saving both audit messages and extracted values atomically.
/// </summary>
public class AuditDataService : IAuditDataService
{
    private readonly IAuditMessageRepository _auditMessageRepository;
    private readonly IExtractedValuesRepository _extractedValuesRepository;
    private readonly ILogger<AuditDataService> _logger;

    public AuditDataService(
        IAuditMessageRepository auditMessageRepository,
        IExtractedValuesRepository extractedValuesRepository,
        ILogger<AuditDataService> logger)
    {
        _auditMessageRepository = auditMessageRepository;
        _extractedValuesRepository = extractedValuesRepository;
        _logger = logger;
    }

    public async Task SaveAuditDataAsync(AuditMessage message, ExtractedData extractedData, int partition, long offset)
    {
        try
        {
            _logger.LogDebug("Saving audit data for message {MessageId}", message.Id);

            // Save audit message (MERGE operation - insert or update)
            await _auditMessageRepository.SaveAsync(message, partition, offset);

            // Save extracted values (DELETE old + INSERT new)
            await _extractedValuesRepository.SaveExtractedValuesAsync(
                message.Id,
                extractedData.ExtractedFields);

            _logger.LogInformation(
                "Successfully saved audit message {MessageId} with {FieldCount} extracted fields",
                message.Id,
                extractedData.ExtractedFields.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving audit data for message {MessageId}", message.Id);
            throw;
        }
    }
}
