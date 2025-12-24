using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for managing case extractions.
/// </summary>
public interface ICaseExtractionRepository
{
    /// <summary>
    /// Creates multiple case extractions in a single transaction.
    /// </summary>
    /// <param name="extractions">The list of extractions to create</param>
    /// <returns>The number of extractions created</returns>
    Task<int> CreateBatchAsync(List<CaseExtraction> extractions);

    /// <summary>
    /// Gets all extractions for a specific case.
    /// </summary>
    /// <param name="caseId">The case ID</param>
    /// <returns>List of extractions for the case</returns>
    Task<List<CaseExtraction>> GetByCaseIdAsync(string caseId);

    /// <summary>
    /// Gets all extractions for a specific audit log.
    /// </summary>
    /// <param name="auditLogId">The audit log ID</param>
    /// <returns>List of extractions for the audit log</returns>
    Task<List<CaseExtraction>> GetByAuditLogIdAsync(string auditLogId);
}
