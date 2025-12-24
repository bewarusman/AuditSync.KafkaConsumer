using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for managing targets in the database.
/// </summary>
public interface ITargetRepository
{
    /// <summary>
    /// Checks if a target with the given name exists in the database.
    /// </summary>
    /// <param name="targetName">The target name to check</param>
    /// <returns>True if the target exists, false otherwise</returns>
    Task<bool> ExistsAsync(string targetName);

    /// <summary>
    /// Gets a target by its name.
    /// </summary>
    /// <param name="targetName">The target name</param>
    /// <returns>The target entity or null if not found</returns>
    Task<Target?> GetByNameAsync(string targetName);
}
