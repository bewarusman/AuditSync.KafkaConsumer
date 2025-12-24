using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Models;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Service for applying extraction rules to audit messages.
/// </summary>
public interface IExtractionService
{
    /// <summary>
    /// Applies extraction rules to an audit message and returns extracted values.
    /// </summary>
    /// <param name="auditMessage">The audit message to process</param>
    /// <param name="rules">The extraction rules to apply</param>
    /// <returns>List of successfully extracted values</returns>
    Task<List<ExtractedValue>> ApplyRulesAsync(AuditMessage auditMessage, List<ExtractionRule> rules);
}
