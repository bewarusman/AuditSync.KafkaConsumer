namespace AuditSync.OracleConsumer.Domain.Entities;

/// <summary>
/// Represents the result of applying extraction rules to an audit message.
/// Contains extracted field/value pairs.
/// </summary>
public class ExtractedData
{
    public string AuditRecordId { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string SqlText { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary of extracted field names and their values.
    /// Each entry represents a (name, value) pair extracted by a rule.
    /// </summary>
    public Dictionary<string, string> ExtractedFields { get; set; } = new();

    public DateTime ProcessedAt { get; set; }
}
