namespace AuditSync.OracleConsumer.Domain.Entities;

/// <summary>
/// Represents an audit message consumed from Kafka.
/// Maps to the flattened JSON structure with 22 properties.
/// </summary>
public class AuditMessage
{
    public string Id { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public long SessionId { get; set; }
    public int EntryId { get; set; }
    public int Statement { get; set; }
    public string DbUser { get; set; } = string.Empty;
    public string UserHost { get; set; } = string.Empty;
    public string Terminal { get; set; } = string.Empty;
    public int Action { get; set; }
    public int ReturnCode { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AuthPrivileges { get; set; } = string.Empty;
    public string AuthGrantee { get; set; } = string.Empty;
    public string NewOwner { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public string OsUser { get; set; } = string.Empty;
    public string? PrivilegeUsed { get; set; }
    public DateTime Timestamp { get; set; }
    public string BindVariables { get; set; } = string.Empty;
    public string SqlText { get; set; } = string.Empty;
    public DateTime ProducedAt { get; set; }
}
