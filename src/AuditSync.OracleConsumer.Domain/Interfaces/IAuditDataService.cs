using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Service for transactional persistence of audit data.
/// Coordinates saving both audit messages and extracted values atomically.
/// </summary>
public interface IAuditDataService
{
    /// <summary>
    /// Saves audit message and extracted values in a single transaction.
    /// Ensures atomicity: either both succeed or both rollback.
    /// </summary>
    /// <param name="message">The audit message to save</param>
    /// <param name="extractedData">The extracted data to save</param>
    /// <param name="partition">Kafka partition number</param>
    /// <param name="offset">Kafka offset number</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveAuditDataAsync(AuditMessage message, ExtractedData extractedData, int partition, long offset);
}
