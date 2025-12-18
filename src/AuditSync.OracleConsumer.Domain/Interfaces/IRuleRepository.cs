using AuditSync.OracleConsumer.Domain.Models;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for loading extraction rules from the database.
/// </summary>
public interface IRuleRepository
{
    /// <summary>
    /// Gets all active extraction rules for a specific target name.
    /// Joins target_rules with targets table to resolve target name to ID.
    /// </summary>
    /// <param name="targetName">The target name (e.g., "Production Oracle Database")</param>
    /// <returns>List of extraction rules ordered by RULE_ORDER</returns>
    Task<List<ExtractionRule>> GetRulesByTargetAsync(string targetName);
}
