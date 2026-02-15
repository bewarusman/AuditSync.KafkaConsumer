using System.Text.Json;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for managing rule extractions from JavaScript rules engine.
/// Maps to case_extractions table.
/// Each extraction object {value, type, tags} is stored as a separate record.
/// </summary>
public class RuleExtractionRepository : IRuleExtractionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RuleExtractionRepository> _logger;

    public RuleExtractionRepository(string connectionString, ILogger<RuleExtractionRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task InsertExtractionsAsync(
        string caseId,
        List<ExtractionResult> extractions,
        CancellationToken cancellationToken = default)
    {
        if (extractions == null || extractions.Count == 0)
        {
            _logger.LogDebug("No extractions to insert for case {CaseId}", caseId);
            return;
        }

        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Each extraction object becomes one record
            foreach (var extraction in extractions)
            {
                var sql = @"
                    INSERT INTO case_extractions
                    (ID, CASE_ID, AUDIT_LOG_ID, RULE_ID, RULE_NAME, EXTRACTION_TYPE, SOURCE_FIELD, EXTRACTION_VALUE, TAGS, EXTRACTED_AT)
                    VALUES
                    (:Id, :CaseId, :AuditLogId, :RuleId, :RuleName, :ExtractionType, :SourceField, :ExtractionValue, :Tags, SYSTIMESTAMP)";

                // Serialize tags to JSON string
                var tagsJson = JsonSerializer.Serialize(extraction.Tags);

                var extractionId = $"extraction-{Guid.NewGuid()}";

                await connection.ExecuteAsync(sql, new
                {
                    Id = extractionId,  // Unique GUID for each extraction
                    CaseId = caseId,
                    AuditLogId = extraction.AuditLogId ?? string.Empty,
                    RuleId = extraction.RuleId ?? "javascript-rule",
                    RuleName = extraction.RuleName ?? "JavaScript Rules Engine",
                    ExtractionType = extraction.Type ?? "Unknown",  // Use 'type' from JavaScript (e.g., "MSISDN", "IMEI")
                    SourceField = extraction.SourceField ?? "unknown",
                    ExtractionValue = extraction.Value,  // The actual extracted value
                    Tags = tagsJson  // JSON array as string: "[]" or "[\"tag1\",\"tag2\"]"
                });

                _logger.LogDebug(
                    "Inserted extraction: ID={Id}, Value={Value}, Type={Type}, Tags={Tags}",
                    extractionId,
                    extraction.Value,
                    extraction.Type,
                    tagsJson);
            }

            _logger.LogInformation(
                "Inserted {Count} extraction record(s) for case {CaseId}",
                extractions.Count,
                caseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting extractions for case {CaseId}", caseId);
            throw;
        }
    }
}
