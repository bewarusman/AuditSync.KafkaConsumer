using AuditSync.OracleConsumer.Domain.Models;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for loading rules from the RULES table (rules engine).
/// Different from IRuleRepository which is for target_rules table.
/// </summary>
public interface IRulesEngineRepository
{
    /// <summary>
    /// Gets all enabled rules for a specific target name.
    /// Joins RULES with TARGETS table to resolve target name to ID.
    /// </summary>
    /// <param name="targetName">The target name (e.g., "DWH")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of rules ordered by ORDER_POSITION (ascending)</returns>
    Task<List<Rule>> GetRulesByTargetNameAsync(string targetName, CancellationToken cancellationToken = default);
}
