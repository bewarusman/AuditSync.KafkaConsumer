using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for loading extraction rules from the Oracle database.
/// </summary>
public class RuleRepository : IRuleRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RuleRepository> _logger;

    public RuleRepository(string connectionString, ILogger<RuleRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<List<ExtractionRule>> GetRulesByTargetAsync(string targetName)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT r.ID, r.TARGET_ID AS TargetId, t.NAME AS TargetName, r.RULE_NAME AS RuleName,
                       r.SOURCE_FIELD AS SourceField, r.REGEX_PATTERN AS RegexPattern,
                       r.IS_ACTIVE AS IsActive, r.RULE_ORDER AS RuleOrder
                FROM target_rules r
                INNER JOIN targets t ON r.TARGET_ID = t.ID
                WHERE t.NAME = :TargetName
                  AND r.IS_ACTIVE = 1
                ORDER BY r.RULE_ORDER";

            var rules = await connection.QueryAsync<ExtractionRule>(sql, new { TargetName = targetName });
            var rulesList = rules.ToList();

            _logger.LogInformation("Loaded {Count} extraction rules for target: {Target}",
                rulesList.Count, targetName);

            return rulesList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rules for target: {Target}", targetName);
            throw;
        }
    }
}
