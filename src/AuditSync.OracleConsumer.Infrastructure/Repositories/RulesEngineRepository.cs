using System.Text.Json;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for loading rules from the RULES table (JavaScript rules engine).
/// </summary>
public class RulesEngineRepository : IRulesEngineRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RulesEngineRepository> _logger;

    public RulesEngineRepository(string connectionString, ILogger<RulesEngineRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<List<Rule>> GetRulesByTargetNameAsync(string targetName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT r.ID, r.TARGET_ID AS TargetId, r.NAME, r.DESCRIPTION,
                       r.ENABLED, r.CONDITIONS, r.ACTIONS, r.ORDER_POSITION AS OrderPosition,
                       r.CREATED_AT AS CreatedAt, r.UPDATED_AT AS UpdatedAt
                FROM RULES r
                INNER JOIN TARGETS t ON r.TARGET_ID = t.ID
                WHERE t.NAME = :TargetName
                  AND r.ENABLED = 1
                ORDER BY r.ORDER_POSITION ASC";

            var rows = await connection.QueryAsync(sql, new { TargetName = targetName });

            var rules = new List<Rule>();

            foreach (var row in rows)
            {
                var rule = new Rule
                {
                    Id = row.ID,
                    TargetId = row.TARGETID,
                    Name = row.NAME,
                    Description = row.DESCRIPTION ?? string.Empty,
                    Enabled = row.ENABLED == 1,
                    OrderPosition = (int)row.ORDERPOSITION,
                    CreatedAt = row.CREATEDAT,
                    UpdatedAt = row.UPDATEDAT
                };

                // Parse CONDITIONS JSON (CLOB)
                string conditionsJson = await ReadClobAsync(row.CONDITIONS);
                if (!string.IsNullOrEmpty(conditionsJson))
                {
                    try
                    {
                        rule.Conditions = JsonSerializer.Deserialize<List<RuleCondition>>(conditionsJson) ?? new List<RuleCondition>();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse CONDITIONS for rule {RuleId}", rule.Id);
                        rule.Conditions = new List<RuleCondition>();
                    }
                }

                // Parse ACTIONS JSON (CLOB)
                string actionsJson = await ReadClobAsync(row.ACTIONS);
                if (!string.IsNullOrEmpty(actionsJson))
                {
                    try
                    {
                        rule.Actions = JsonSerializer.Deserialize<RuleActions>(actionsJson) ?? new RuleActions();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse ACTIONS for rule {RuleId}", rule.Id);
                        rule.Actions = new RuleActions();
                    }
                }

                rules.Add(rule);
            }

            _logger.LogInformation("Loaded {Count} rules for target: {Target}", rules.Count, targetName);

            return rules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rules for target: {Target}", targetName);
            throw;
        }
    }

    private Task<string> ReadClobAsync(dynamic clobValue)
    {
        if (clobValue == null || clobValue is DBNull)
        {
            return Task.FromResult(string.Empty);
        }

        if (clobValue is string str)
        {
            return Task.FromResult(str);
        }

        if (clobValue is Oracle.ManagedDataAccess.Types.OracleClob clob)
        {
            return Task.FromResult(clob.Value ?? string.Empty);
        }

        return Task.FromResult(clobValue?.ToString() ?? string.Empty);
    }
}
