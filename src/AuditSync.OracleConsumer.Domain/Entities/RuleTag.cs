namespace AuditSync.OracleConsumer.Domain.Entities;

/// <summary>
/// Represents a tag configuration for an extraction rule.
/// Tags are applied to extracted values based on conditions (e.g., StartsWith, Regex, Range).
/// </summary>
public class RuleTag
{
    public string Id { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = string.Empty;
    public string ConditionValue { get; set; } = string.Empty;
    public int TagPriority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
