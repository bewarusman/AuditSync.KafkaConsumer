using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Repository for managing rule tag configurations.
/// </summary>
public interface IRuleTagRepository
{
    /// <summary>
    /// Gets all active tag rules for a specific extraction rule.
    /// </summary>
    /// <param name="ruleId">The extraction rule ID</param>
    /// <returns>List of active tag rules ordered by priority</returns>
    Task<List<RuleTag>> GetActiveTagRulesByRuleIdAsync(string ruleId);

    /// <summary>
    /// Gets all active tag rules for multiple extraction rules.
    /// Useful for batch loading tag rules for a target.
    /// </summary>
    /// <param name="ruleIds">List of extraction rule IDs</param>
    /// <returns>Dictionary mapping rule ID to list of tag rules</returns>
    Task<Dictionary<string, List<RuleTag>>> GetActiveTagRulesByRuleIdsAsync(List<string> ruleIds);
}
