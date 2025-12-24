using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for managing case extractions in Oracle database.
/// </summary>
public class CaseExtractionRepository : ICaseExtractionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CaseExtractionRepository> _logger;

    public CaseExtractionRepository(string connectionString, ILogger<CaseExtractionRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<int> CreateBatchAsync(List<CaseExtraction> extractions)
    {
        if (extractions == null || extractions.Count == 0)
        {
            return 0;
        }

        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO case_extractions (ID, CASE_ID, AUDIT_LOG_ID, RULE_ID, RULE_NAME,
                                              REGEX_PATTERN, SOURCE_FIELD, FIELD_VALUE, EXTRACTED_AT)
                VALUES (:Id, :CaseId, :AuditLogId, :RuleId, :RuleName,
                        :RegexPattern, :SourceField, :FieldValue, :ExtractedAt)";

            var rowsAffected = await connection.ExecuteAsync(sql, extractions);

            _logger.LogDebug("Created {Count} case extractions for case {CaseId}",
                rowsAffected, extractions.First().CaseId);

            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch of case extractions");
            throw;
        }
    }

    public async Task<List<CaseExtraction>> GetByCaseIdAsync(string caseId)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT ID, CASE_ID as CaseId, AUDIT_LOG_ID as AuditLogId, RULE_ID as RuleId,
                       RULE_NAME as RuleName, REGEX_PATTERN as RegexPattern, SOURCE_FIELD as SourceField,
                       FIELD_VALUE as FieldValue, EXTRACTED_AT as ExtractedAt
                FROM case_extractions
                WHERE CASE_ID = :CaseId
                ORDER BY EXTRACTED_AT";

            var result = await connection.QueryAsync<CaseExtraction>(sql, new { CaseId = caseId });

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting case extractions for case {CaseId}", caseId);
            throw;
        }
    }

    public async Task<List<CaseExtraction>> GetByAuditLogIdAsync(string auditLogId)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT ID, CASE_ID as CaseId, AUDIT_LOG_ID as AuditLogId, RULE_ID as RuleId,
                       RULE_NAME as RuleName, REGEX_PATTERN as RegexPattern, SOURCE_FIELD as SourceField,
                       FIELD_VALUE as FieldValue, EXTRACTED_AT as ExtractedAt
                FROM case_extractions
                WHERE AUDIT_LOG_ID = :AuditLogId
                ORDER BY EXTRACTED_AT";

            var result = await connection.QueryAsync<CaseExtraction>(sql, new { AuditLogId = auditLogId });

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting case extractions for audit log {AuditLogId}", auditLogId);
            throw;
        }
    }
}
