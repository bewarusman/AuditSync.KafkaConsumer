using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Exceptions;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Rule engine that applies regex-based extraction rules to audit messages.
/// Rules are loaded lazily on first use per target and cached in memory.
/// </summary>
public class RegexRuleEngine : IRuleEngine
{
    private readonly IRuleRepository _ruleRepository;
    private readonly ILogger<RegexRuleEngine> _logger;
    private readonly Dictionary<string, List<ExtractionRule>> _ruleCache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public RegexRuleEngine(IRuleRepository ruleRepository, ILogger<RegexRuleEngine> logger)
    {
        _ruleRepository = ruleRepository;
        _logger = logger;
        _ruleCache = new Dictionary<string, List<ExtractionRule>>();
    }

    public async Task<ExtractedData> ApplyRulesAsync(AuditMessage message)
    {
        var extractedData = new ExtractedData
        {
            AuditRecordId = message.Id,
            Schema = message.Owner,
            TableName = message.Name,
            SqlText = message.SqlText,
            ExtractedFields = new Dictionary<string, string>(),
            ProcessedAt = DateTime.UtcNow
        };

        // Lazy load rules: check cache first, load from DB only if rules not found
        var rules = await GetRulesForTargetAsync(message.Target);

        if (rules == null || rules.Count == 0)
        {
            _logger.LogWarning("No rules found for target: {Target}", message.Target);
            return extractedData;
        }

        // Apply each rule
        foreach (var rule in rules)
        {
            var sourceValue = GetSourceValue(message, rule.SourceField);
            var match = Regex.Match(sourceValue ?? string.Empty, rule.RegexPattern);

            if (match.Success && match.Groups.Count > 1)
            {
                extractedData.ExtractedFields[rule.RuleName] = match.Groups[1].Value;
                _logger.LogDebug("Rule '{RuleName}' extracted value: {Value}",
                    rule.RuleName, match.Groups[1].Value);
            }
            else if (rule.IsRequired)
            {
                throw new RuleValidationException(
                    $"Required rule '{rule.RuleName}' failed to match for target '{message.Target}'");
            }
            else
            {
                _logger.LogDebug("Optional rule '{RuleName}' did not match", rule.RuleName);
            }
        }

        _logger.LogInformation("Extracted {Count} field(s) for message {MessageId}",
            extractedData.ExtractedFields.Count, message.Id);

        return extractedData;
    }

    private async Task<List<ExtractionRule>> GetRulesForTargetAsync(string target)
    {
        // Check cache first (read without lock for performance)
        if (_ruleCache.TryGetValue(target, out var cachedRules))
        {
            return cachedRules;
        }

        // Rule not in cache - load from database
        await _cacheLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another thread might have loaded it)
            if (_ruleCache.TryGetValue(target, out var doubleCheckedRules))
            {
                return doubleCheckedRules;
            }

            // Load from database
            var rules = await _ruleRepository.GetRulesByTargetAsync(target);

            // Add to cache for future use
            _ruleCache[target] = rules;

            _logger.LogInformation("Loaded and cached {Count} rules for target: {Target}",
                rules.Count, target);

            return rules;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private string? GetSourceValue(AuditMessage message, string sourceField)
    {
        // Access properties from flattened structure directly
        return sourceField.ToLower() switch
        {
            "sqltext" => message.SqlText,
            "bindvariables" => message.BindVariables,
            "owner" => message.Owner,
            "name" => message.Name,
            "dbuser" => message.DbUser,
            "userhost" => message.UserHost,
            "terminal" => message.Terminal,
            "osuser" => message.OsUser,
            "target" => message.Target,
            "authprivileges" => message.AuthPrivileges,
            "authgrantee" => message.AuthGrantee,
            "newowner" => message.NewOwner,
            "newname" => message.NewName,
            "privilegeused" => message.PrivilegeUsed,
            _ => null
        };
    }
}
