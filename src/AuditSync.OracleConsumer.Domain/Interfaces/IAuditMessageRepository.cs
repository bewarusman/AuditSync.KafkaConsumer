using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for storing complete audit messages from Kafka.
/// </summary>
public interface IAuditMessageRepository
{
    /// <summary>
    /// Saves an audit message to the database using MERGE (upsert) logic.
    /// On duplicate: updates all fields and increments PROCESS_COUNTER.
    /// On new: inserts with PROCESS_COUNTER = 1.
    /// </summary>
    /// <param name="message">The audit message to save</param>
    /// <param name="partition">Kafka partition number</param>
    /// <param name="offset">Kafka offset number</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveAsync(AuditMessage message, int partition, long offset);

    /// <summary>
    /// Checks if a message has already been processed.
    /// </summary>
    /// <param name="messageId">The message ID to check</param>
    /// <returns>True if the message exists in the database</returns>
    Task<bool> IsProcessedAsync(string messageId);
}
