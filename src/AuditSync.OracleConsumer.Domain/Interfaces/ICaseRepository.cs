using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for managing cases.
/// </summary>
public interface ICaseRepository
{
    /// <summary>
    /// Creates a new case for an audit log.
    /// </summary>
    /// <param name="case">The case to create</param>
    /// <returns>The created case ID</returns>
    Task<string> CreateAsync(Case @case);

    /// <summary>
    /// Checks if a case already exists for a given audit log ID.
    /// </summary>
    /// <param name="auditLogId">The audit log ID to check</param>
    /// <returns>True if a case exists for this audit log</returns>
    Task<bool> ExistsForAuditLogAsync(string auditLogId);

    /// <summary>
    /// Gets a case by its ID.
    /// </summary>
    /// <param name="caseId">The case ID</param>
    /// <returns>The case, or null if not found</returns>
    Task<Case?> GetByIdAsync(string caseId);

    /// <summary>
    /// Gets a case by its audit log ID.
    /// </summary>
    /// <param name="auditLogId">The audit log ID</param>
    /// <returns>The case, or null if not found</returns>
    Task<Case?> GetByAuditLogIdAsync(string auditLogId);
}
