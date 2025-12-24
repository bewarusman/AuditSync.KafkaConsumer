namespace AuditSync.OracleConsumer.Domain.Entities;

/// <summary>
/// Represents an extracted value for a case, with denormalized rule information.
/// Each row stores one extracted value and the rule that extracted it (rule name, pattern, source field).
/// </summary>
public class CaseExtraction
{
    public string Id { get; set; } = string.Empty;
    public string CaseId { get; set; } = string.Empty;
    public string AuditLogId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;  // Denormalized for easy querying
    public string RegexPattern { get; set; } = string.Empty;  // Denormalized for audit trail
    public string SourceField { get; set; } = string.Empty;  // Where the value was extracted from
    public string? FieldValue { get; set; }  // The actual extracted value
    public DateTime ExtractedAt { get; set; }
}
