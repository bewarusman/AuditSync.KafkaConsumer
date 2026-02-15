using AuditSync.OracleConsumer.Domain.Models;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for managing rule extractions (from JavaScript rules engine).
/// Maps to RULE_EXTRACTIONS table.
/// </summary>
public interface IRuleExtractionRepository
{
    /// <summary>
    /// Inserts multiple extractions for a case.
    /// </summary>
    /// <param name="caseId">The case ID</param>
    /// <param name="extractions">List of extraction results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InsertExtractionsAsync(
        string caseId,
        List<ExtractionResult> extractions,
        CancellationToken cancellationToken = default);
}
