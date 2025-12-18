namespace AuditSync.OracleConsumer.Domain.Models;

/// <summary>
/// Represents the result of applying a single extraction rule.
/// </summary>
public class RuleResult
{
    public string RuleName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public bool Success { get; set; }
}
