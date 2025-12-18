namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for storing extracted field/value pairs.
/// </summary>
public interface IExtractedValuesRepository
{
    /// <summary>
    /// Saves extracted values for an audit message.
    /// On duplicate message: deletes old values and inserts new ones.
    /// On new message: inserts all key-value pairs as separate rows.
    /// </summary>
    /// <param name="auditMessageId">The audit message ID (foreign key)</param>
    /// <param name="extractedFields">Dictionary of field names and values</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveExtractedValuesAsync(string auditMessageId, Dictionary<string, string> extractedFields);
}
