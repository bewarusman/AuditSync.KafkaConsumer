using AuditSync.OracleConsumer.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for storing extracted field/value pairs in Oracle database.
/// On duplicate message: deletes old values and inserts new ones.
/// </summary>
public class ExtractedValuesRepository : IExtractedValuesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ExtractedValuesRepository> _logger;

    public ExtractedValuesRepository(string connectionString, ILogger<ExtractedValuesRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SaveExtractedValuesAsync(string auditMessageId, Dictionary<string, string> extractedFields)
    {
        if (extractedFields == null || extractedFields.Count == 0)
        {
            _logger.LogDebug("No extracted values to save for message {MessageId}", auditMessageId);
            return;
        }

        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            // Delete existing extracted values for this message (in case of duplicate/reprocessing)
            var deleteSql = "DELETE FROM audit_log_extracted_values WHERE AUDIT_MESSAGE_ID = :AuditMessageId";
            var deletedCount = await connection.ExecuteAsync(deleteSql, new { AuditMessageId = auditMessageId });

            if (deletedCount > 0)
            {
                _logger.LogDebug("Deleted {Count} existing extracted values for message {MessageId}",
                    deletedCount, auditMessageId);
            }

            // Insert new extracted values
            var insertSql = @"
                INSERT INTO audit_log_extracted_values (ID, AUDIT_MESSAGE_ID, FIELD_NAME, FIELD_VALUE, EXTRACTED_AT)
                VALUES (SYS_GUID(), :AuditMessageId, :FieldName, :FieldValue, SYSTIMESTAMP)";

            var values = extractedFields.Select(kvp => new
            {
                AuditMessageId = auditMessageId,
                FieldName = kvp.Key,
                FieldValue = kvp.Value
            }).ToList();

            var insertedCount = await connection.ExecuteAsync(insertSql, values);

            _logger.LogInformation("Inserted {Count} extracted values for message {MessageId}",
                insertedCount, auditMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving extracted values for message {MessageId}", auditMessageId);
            throw;
        }
    }
}
