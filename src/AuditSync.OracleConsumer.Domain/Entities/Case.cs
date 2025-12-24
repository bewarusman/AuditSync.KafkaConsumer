namespace AuditSync.OracleConsumer.Domain.Entities;

/// <summary>
/// Represents a case created when extraction rules successfully extract values from an audit log.
/// Cases are created when ANY extraction rule matches and extracts a value.
/// </summary>
public class Case
{
    public string Id { get; set; } = string.Empty;
    public string AuditLogId { get; set; } = string.Empty;
    public string CaseStatus { get; set; } = "OPEN";  // OPEN, RESOLVED, ASSIGNED
    public string? Valid { get; set; }  // YES, NO, or NULL
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
}
