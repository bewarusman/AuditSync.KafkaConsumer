using AuditSync.OracleConsumer.Domain.Interfaces;
using System.Collections.Concurrent;

namespace AuditSync.OracleConsumer.Infrastructure.Kafka;

/// <summary>
/// Manages Kafka offset tracking in memory.
/// </summary>
public class OffsetManager : IOffsetManager
{
    private readonly ConcurrentDictionary<int, long> _offsets = new();

    public void StoreOffset(int partition, long offset)
    {
        _offsets[partition] = offset;
    }

    public long? GetLastOffset(int partition)
    {
        return _offsets.TryGetValue(partition, out var offset) ? offset : null;
    }
}
