namespace AuditSync.OracleConsumer.Domain.Models;

/// <summary>
/// Represents a single extracted value with its associated rule information.
/// Used as a DTO to transfer extraction results between services.
/// </summary>
public class ExtractedValue
{
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RegexPattern { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
