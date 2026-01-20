using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for managing rule tag configurations in Oracle database.
/// </summary>
public class RuleTagRepository : IRuleTagRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RuleTagRepository> _logger;

    public RuleTagRepository(string connectionString, ILogger<RuleTagRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<List<RuleTag>> GetActiveTagRulesByRuleIdAsync(string ruleId)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT
                    ID as Id,
                    RULE_ID as RuleId,
                    TAG_NAME as TagName,
                    CONDITION_TYPE as ConditionType,
                    CONDITION_VALUE as ConditionValue,
                    TAG_PRIORITY as TagPriority,
                    CASE WHEN IS_ACTIVE = 1 THEN 1 ELSE 0 END as IsActive,
                    CREATED_AT as CreatedAt,
                    UPDATED_AT as UpdatedAt
                FROM rule_tags
                WHERE RULE_ID = :RuleId
                  AND IS_ACTIVE = 1
                ORDER BY TAG_PRIORITY DESC";

            var tagRules = await connection.QueryAsync<RuleTag>(sql, new { RuleId = ruleId });

            var result = tagRules.ToList();
            _logger.LogDebug("Loaded {Count} active tag rule(s) for rule {RuleId}", result.Count, ruleId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tag rules for rule {RuleId}", ruleId);
            throw;
        }
    }

    public async Task<Dictionary<string, List<RuleTag>>> GetActiveTagRulesByRuleIdsAsync(List<string> ruleIds)
    {
        try
        {
            if (ruleIds == null || ruleIds.Count == 0)
            {
                return new Dictionary<string, List<RuleTag>>();
            }

            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            // Build IN clause for multiple rule IDs
            var ruleIdParams = string.Join(",", ruleIds.Select((_, i) => $":RuleId{i}"));
            var sql = $@"
                SELECT
                    ID as Id,
                    RULE_ID as RuleId,
                    TAG_NAME as TagName,
                    CONDITION_TYPE as ConditionType,
                    CONDITION_VALUE as ConditionValue,
                    TAG_PRIORITY as TagPriority,
                    CASE WHEN IS_ACTIVE = 1 THEN 1 ELSE 0 END as IsActive,
                    CREATED_AT as CreatedAt,
                    UPDATED_AT as UpdatedAt
                FROM rule_tags
                WHERE RULE_ID IN ({ruleIdParams})
                  AND IS_ACTIVE = 1
                ORDER BY RULE_ID, TAG_PRIORITY DESC";

            // Create dynamic parameters
            var parameters = new DynamicParameters();
            for (int i = 0; i < ruleIds.Count; i++)
            {
                parameters.Add($"RuleId{i}", ruleIds[i]);
            }

            var tagRules = await connection.QueryAsync<RuleTag>(sql, parameters);

            // Group by RuleId
            var result = tagRules
                .GroupBy(t => t.RuleId)
                .ToDictionary(g => g.Key, g => g.ToList());

            _logger.LogDebug("Loaded tag rules for {RuleCount} rule(s), total {TagCount} tag rules",
                result.Count, tagRules.Count());

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tag rules for multiple rules");
            throw;
        }
    }
}
