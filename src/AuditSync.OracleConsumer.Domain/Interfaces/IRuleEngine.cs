using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Contract for processing audit messages and extracting data using rules.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Applies extraction rules to an audit message and returns extracted data.
    /// Rules are loaded lazily on first use per target and cached in memory.
    /// </summary>
    /// <param name="message">The audit message to process</param>
    /// <returns>Extracted data with field/value pairs</returns>
    Task<ExtractedData> ApplyRulesAsync(AuditMessage message);
}
