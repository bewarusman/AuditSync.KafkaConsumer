namespace AuditSync.OracleConsumer.Domain.Entities;

/// <summary>
/// Represents a target database being audited.
/// </summary>
public class Target
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
