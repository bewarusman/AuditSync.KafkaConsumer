namespace AuditSync.OracleConsumer.Domain.Models;

/// <summary>
/// Represents a rule loaded from the database.
/// Maps to RULES table.
/// </summary>
public class Rule
{
    /// <summary>
    /// Rule ID: rule-{guid}
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Target system ID
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Rule name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Rule description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the rule is enabled (1=enabled, 0=disabled)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// JSON array of conditions
    /// </summary>
    public List<RuleCondition> Conditions { get; set; } = new();

    /// <summary>
    /// Actions to take when rule matches
    /// </summary>
    public RuleActions Actions { get; set; } = new();

    /// <summary>
    /// Rule priority (lower = higher priority)
    /// </summary>
    public int OrderPosition { get; set; }

    /// <summary>
    /// When the rule was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the rule was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents a single condition within a rule.
/// </summary>
public class RuleCondition
{
    /// <summary>
    /// Field name to evaluate (e.g., "dbUser", "sqlText")
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Operator to use for comparison
    /// </summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// Value to compare against (can be string, number, array, or object)
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Whether this condition should trigger extraction
    /// </summary>
    public bool Extract { get; set; }

    /// <summary>
    /// Configuration for extraction logic
    /// </summary>
    public ExtractionConfig? ExtractConfig { get; set; }

    /// <summary>
    /// Order of evaluation within the rule
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Configuration for JavaScript extraction.
/// </summary>
public class ExtractionConfig
{
    /// <summary>
    /// Script type (currently only "javascript" is supported)
    /// </summary>
    public string Script { get; set; } = "javascript";

    /// <summary>
    /// JavaScript code to execute for extraction
    /// </summary>
    public string ExtractionLogic { get; set; } = string.Empty;
}

/// <summary>
/// Actions to execute when a rule matches.
/// </summary>
public class RuleActions
{
    /// <summary>
    /// Whether to create a case when this rule matches
    /// </summary>
    public bool CreateCase { get; set; }

    /// <summary>
    /// Notification channels (for future use)
    /// </summary>
    public List<string> NotifyChannels { get; set; } = new();
}

/// <summary>
/// Result of executing extraction logic.
/// </summary>
public class ExtractionResult
{
    /// <summary>
    /// The extracted value (e.g., "9647508282748")
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Type of extraction (e.g., "MSISDN", "IMEI")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// List of tags associated with this extraction
    /// </summary>
    public List<string> Tags { get; set; } = new();

    // Additional fields for case_extractions table compatibility

    /// <summary>
    /// Audit log ID (populated by consumer)
    /// </summary>
    public string? AuditLogId { get; set; }

    /// <summary>
    /// Rule ID that extracted this value (populated by consumer)
    /// </summary>
    public string? RuleId { get; set; }

    /// <summary>
    /// Rule name (populated by consumer)
    /// </summary>
    public string? RuleName { get; set; }

    /// <summary>
    /// Regex pattern or extraction description (populated by consumer)
    /// </summary>
    public string? RegexPattern { get; set; }

    /// <summary>
    /// Source field name (e.g., "sqlText", "bindVariables")
    /// </summary>
    public string? SourceField { get; set; }
}

/// <summary>
/// Result of evaluating rules against an audit log.
/// </summary>
public class RuleMatchResult
{
    /// <summary>
    /// ID of the matched rule
    /// </summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the matched rule
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the rule matched
    /// </summary>
    public bool Matched { get; set; }

    /// <summary>
    /// Actions to execute
    /// </summary>
    public RuleActions Actions { get; set; } = new();

    /// <summary>
    /// Extracted values from the rule
    /// </summary>
    public List<ExtractionResult> Extractions { get; set; } = new();
}
