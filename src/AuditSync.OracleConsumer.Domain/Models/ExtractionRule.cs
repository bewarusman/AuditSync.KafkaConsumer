namespace AuditSync.OracleConsumer.Domain.Models;

/// <summary>
/// Represents a single extraction rule loaded from the database.
/// Each rule defines how to extract a value from an audit message field using regex.
/// </summary>
public class ExtractionRule
{
    public string Id { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the extracted field (used as key in name-value pair).
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// The source field in the audit message (e.g., "text", "bindVariables", "owner", "name").
    /// </summary>
    public string SourceField { get; set; } = string.Empty;

    /// <summary>
    /// The regex pattern used to extract the value.
    /// First capturing group is used as the extracted value.
    /// </summary>
    public string RegexPattern { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this rule must match for processing to succeed.
    /// If true and the rule fails to match, an exception is thrown.
    /// </summary>
    public bool IsRequired { get; set; }

    public bool IsActive { get; set; }
    public int RuleOrder { get; set; }
}
