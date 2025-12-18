namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Contract for managing Kafka offset commits.
/// </summary>
public interface IOffsetManager
{
    /// <summary>
    /// Stores the last committed offset for a partition.
    /// </summary>
    /// <param name="partition">Kafka partition number</param>
    /// <param name="offset">Kafka offset number</param>
    void StoreOffset(int partition, long offset);

    /// <summary>
    /// Retrieves the last committed offset for a partition.
    /// </summary>
    /// <param name="partition">Kafka partition number</param>
    /// <returns>The last committed offset, or null if none exists</returns>
    long? GetLastOffset(int partition);
}
