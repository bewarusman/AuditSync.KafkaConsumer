using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Models;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Service for managing cases and their extractions.
/// </summary>
public interface ICaseService
{
    /// <summary>
    /// Creates a case with extracted values for an audit log.
    /// Handles reprocessing by checking if case already exists.
    /// </summary>
    /// <param name="auditMessage">The audit message</param>
    /// <param name="extractedValues">The extracted values to store</param>
    /// <returns>The created case ID, or null if case already exists</returns>
    Task<string?> CreateCaseWithExtractionsAsync(AuditMessage auditMessage, List<ExtractedValue> extractedValues);
}
