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
    /// The source field in the audit message (0=SqlText, 1=BindVariables, etc.).
    /// </summary>
    public int SourceField { get; set; }

    /// <summary>
    /// The regex pattern used to extract the value.
    /// First capturing group is used as the extracted value.
    /// </summary>
    public string RegexPattern { get; set; } = string.Empty;

    /// <summary>
    /// Whether this rule is required (0=optional, 1=required).
    /// </summary>
    public int IsRequired { get; set; }

    public int RuleOrder { get; set; }
}
