using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Service for managing cases and their extractions.
/// Handles case creation with denormalized extraction data.
/// </summary>
public class CaseService : ICaseService
{
    private readonly ICaseRepository _caseRepository;
    private readonly ICaseExtractionRepository _caseExtractionRepository;
    private readonly ILogger<CaseService> _logger;

    public CaseService(
        ICaseRepository caseRepository,
        ICaseExtractionRepository caseExtractionRepository,
        ILogger<CaseService> logger)
    {
        _caseRepository = caseRepository;
        _caseExtractionRepository = caseExtractionRepository;
        _logger = logger;
    }

    public async Task<string?> CreateCaseWithExtractionsAsync(
        AuditMessage auditMessage,
        List<ExtractedValue> extractedValues)
    {
        if (extractedValues == null || extractedValues.Count == 0)
        {
            _logger.LogDebug("No extracted values provided for message {MessageId}", auditMessage.Id);
            return null;
        }

        try
        {
            // Check if case already exists (reprocessing scenario)
            var caseExists = await _caseRepository.ExistsForAuditLogAsync(auditMessage.Id);

            if (caseExists)
            {
                _logger.LogInformation(
                    "Case already exists for audit log {AuditLogId}, skipping case creation (reprocessing)",
                    auditMessage.Id);
                return null;
            }

            // Create new case
            var caseId = Guid.NewGuid().ToString();
            var newCase = new Case
            {
                Id = caseId,
                AuditLogId = auditMessage.Id,
                CaseStatus = "OPEN",
                Valid = null,  // Always NULL when case is created (for manual review later)
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ResolvedAt = null,
                ResolvedBy = null,
                ResolutionNotes = null
            };

            await _caseRepository.CreateAsync(newCase);

            _logger.LogInformation("Created case {CaseId} for audit log {AuditLogId}",
                caseId, auditMessage.Id);

            // Create case extractions with denormalized rule information
            var caseExtractions = extractedValues.Select(ev => new CaseExtraction
            {
                Id = Guid.NewGuid().ToString(),
                CaseId = caseId,
                AuditLogId = auditMessage.Id,
                RuleId = ev.RuleId,
                RuleName = ev.RuleName,  // Denormalized
                RegexPattern = ev.RegexPattern,  // Denormalized
                SourceField = ev.SourceField,  // Denormalized
                FieldValue = ev.Value,
                ExtractedAt = DateTime.UtcNow
            }).ToList();

            var extractionsCreated = await _caseExtractionRepository.CreateBatchAsync(caseExtractions);

            _logger.LogInformation(
                "Created {Count} extraction(s) for case {CaseId}",
                extractionsCreated,
                caseId);

            return caseId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating case with extractions for audit log {AuditLogId}",
                auditMessage.Id);
            throw;
        }
    }
}
