namespace AuditSync.OracleConsumer.Domain.Entities;

/// <summary>
/// Represents an extraction result from rules engine stored in the database.
/// Maps to RULE_EXTRACTIONS table.
/// </summary>
public class RuleExtractionEntity
{
    /// <summary>
    /// Primary key: extraction-{guid}
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to cases.ID
    /// </summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>
    /// The actual extracted value (e.g., "9647508282748")
    /// </summary>
    public string ExtractionValue { get; set; } = string.Empty;

    /// <summary>
    /// Type of extraction (e.g., "MSISDN", "IMEI", "IMSI")
    /// </summary>
    public string ExtractionType { get; set; } = string.Empty;

    /// <summary>
    /// JSON string array of tags (e.g., "[]" or "[\"suspicious\",\"vip\"]")
    /// </summary>
    public string ExtractionTags { get; set; } = "[]";

    /// <summary>
    /// Timestamp when extraction was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
