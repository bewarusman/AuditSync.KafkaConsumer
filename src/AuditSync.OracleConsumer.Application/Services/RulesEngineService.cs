using System.Text.Json;
using System.Text.RegularExpressions;
using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Service for evaluating rules against audit messages.
/// Implements short-circuit evaluation and JavaScript extraction.
/// </summary>
public class RulesEngineService
{
    private readonly JavaScriptExtractor _jsExtractor;
    private readonly ILogger<RulesEngineService> _logger;

    public RulesEngineService(
        JavaScriptExtractor jsExtractor,
        ILogger<RulesEngineService> logger)
    {
        _jsExtractor = jsExtractor;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates rules against an audit log.
    /// Returns the first matching rule (short-circuit logic).
    /// </summary>
    public async Task<RuleMatchResult?> EvaluateRulesAsync(
        List<Rule> rules,
        AuditMessage auditLog,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Evaluating {Count} rules for audit log {AuditLogId}", rules.Count, auditLog.Id);

        // Rules are already sorted by ORDER_POSITION in cache
        foreach (var rule in rules)
        {
            var matchResult = await EvaluateRuleAsync(rule, auditLog, cancellationToken);

            if (matchResult != null && matchResult.Matched)
            {
                _logger.LogInformation(
                    "Rule '{RuleName}' matched for audit log {AuditLogId} with {ExtractionCount} extractions",
                    rule.Name,
                    auditLog.Id,
                    matchResult.Extractions.Count);

                return matchResult; // Stop on first match
            }
        }

        _logger.LogDebug("No rules matched for audit log {AuditLogId}", auditLog.Id);
        return null;
    }

    /// <summary>
    /// Evaluates a single rule against an audit log.
    /// Returns null if rule doesn't match.
    /// </summary>
    private async Task<RuleMatchResult?> EvaluateRuleAsync(
        Rule rule,
        AuditMessage auditLog,
        CancellationToken cancellationToken)
    {
        // Sort conditions by order (ascending)
        var sortedConditions = rule.Conditions.OrderBy(c => c.Order).ToList();

        var allExtractions = new List<ExtractionResult>();

        // Evaluate conditions with short-circuit logic
        foreach (var condition in sortedConditions)
        {
            var conditionResult = EvaluateCondition(condition, auditLog, out var extractions);

            if (!conditionResult)
            {
                // Condition failed - short circuit, rule doesn't match
                _logger.LogDebug(
                    "Rule '{RuleName}' condition failed: {Field} {Operator}",
                    rule.Name,
                    condition.Field,
                    condition.Operator);

                return null;
            }

            // Condition passed, collect extractions if any
            if (extractions != null && extractions.Count > 0)
            {
                // Set source field for each extraction (Type is already set by JavaScript)
                foreach (var extraction in extractions)
                {
                    extraction.SourceField = condition.Field;
                    // Note: Type comes from JavaScript {value, type, tags}
                    // It will be stored in REGEX_PATTERN column
                }

                allExtractions.AddRange(extractions);
                _logger.LogDebug(
                    "Executed extraction for rule '{RuleName}', found {Count} items",
                    rule.Name,
                    extractions.Count);
            }
        }

        // All conditions passed
        return new RuleMatchResult
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            Matched = true,
            Actions = rule.Actions,
            Extractions = allExtractions
        };
    }

    /// <summary>
    /// Evaluates a single condition against an audit log.
    /// Returns true if condition matches, false otherwise.
    /// </summary>
    private bool EvaluateCondition(
        RuleCondition condition,
        AuditMessage auditLog,
        out List<ExtractionResult>? extractions)
    {
        extractions = null;

        // Get the actual value from the audit log
        var actualValue = GetFieldValue(condition.Field, auditLog);

        // Evaluate operator
        bool matches = condition.Operator.ToLower() switch
        {
            "equals" => EvaluateEquals(actualValue, condition.Value),
            "not_equals" => !EvaluateEquals(actualValue, condition.Value),
            "contains" => EvaluateContains(actualValue, condition.Value),
            "regex" => EvaluateRegex(actualValue, condition.Value),
            "in" => EvaluateIn(actualValue, condition.Value),
            "not_in" => !EvaluateIn(actualValue, condition.Value),
            "gt" => EvaluateGreaterThan(actualValue, condition.Value),
            "lt" => EvaluateLessThan(actualValue, condition.Value),
            "gte" => EvaluateGreaterThanOrEqual(actualValue, condition.Value),
            "lte" => EvaluateLessThanOrEqual(actualValue, condition.Value),
            _ => throw new NotSupportedException($"Operator '{condition.Operator}' is not supported")
        };

        if (!matches)
        {
            return false;
        }

        // If condition matches and has extraction config, execute JavaScript
        if (condition.Extract && condition.ExtractConfig != null)
        {
            extractions = _jsExtractor.ExecuteExtraction(
                condition.ExtractConfig.ExtractionLogic,
                actualValue ?? string.Empty,
                auditLog);
        }

        return true;
    }

    /// <summary>
    /// Gets field value from audit message by field name.
    /// </summary>
    private string? GetFieldValue(string fieldName, AuditMessage auditLog)
    {
        return fieldName.ToLower() switch
        {
            "dbuser" => auditLog.DbUser,
            "action" => auditLog.Action.ToString(),
            "owner" => auditLog.Owner,
            "name" => auditLog.Name,
            "sqltext" => auditLog.SqlText,
            "bindvariables" => auditLog.BindVariables,
            "osuser" => auditLog.OsUser,
            "userhost" => auditLog.UserHost,
            "terminal" => auditLog.Terminal,
            "returncode" => auditLog.ReturnCode.ToString(),
            _ => null
        };
    }

    #region Operator Implementations

    private bool EvaluateEquals(string? actual, object? expected)
    {
        if (actual == null && expected == null) return true;
        if (actual == null || expected == null) return false;

        return string.Equals(actual, expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private bool EvaluateContains(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        return actual.Contains(expected.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    private bool EvaluateRegex(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        try
        {
            var regex = new Regex(expected.ToString()!, RegexOptions.None, TimeSpan.FromSeconds(1));
            return regex.IsMatch(actual);
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "Regex timeout for pattern: {Pattern}", expected);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Regex error for pattern: {Pattern}", expected);
            return false;
        }
    }

    private bool EvaluateIn(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        List<string> expectedValues;

        // Check if expected is a JSON array
        if (expected is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            expectedValues = jsonElement.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .ToList();
        }
        // Check if expected is a C# array/list
        else if (expected is System.Collections.IEnumerable enumerable && expected is not string)
        {
            expectedValues = enumerable.Cast<object>()
                .Select(o => o?.ToString() ?? string.Empty)
                .ToList();
        }
        // Check if expected is a CSV string
        else if (expected.ToString()!.Contains(','))
        {
            expectedValues = expected.ToString()!
                .Split(',')
                .Select(v => v.Trim())
                .ToList();
        }
        else
        {
            // Single value
            expectedValues = new List<string> { expected.ToString()! };
        }

        return expectedValues.Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase));
    }

    private bool EvaluateGreaterThan(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (double.TryParse(actual, out var actualNum) &&
            double.TryParse(expected.ToString(), out var expectedNum))
        {
            return actualNum > expectedNum;
        }

        return false;
    }

    private bool EvaluateLessThan(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (double.TryParse(actual, out var actualNum) &&
            double.TryParse(expected.ToString(), out var expectedNum))
        {
            return actualNum < expectedNum;
        }

        return false;
    }

    private bool EvaluateGreaterThanOrEqual(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (double.TryParse(actual, out var actualNum) &&
            double.TryParse(expected.ToString(), out var expectedNum))
        {
            return actualNum >= expectedNum;
        }

        return false;
    }

    private bool EvaluateLessThanOrEqual(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (double.TryParse(actual, out var actualNum) &&
            double.TryParse(expected.ToString(), out var expectedNum))
        {
            return actualNum <= expectedNum;
        }

        return false;
    }

    #endregion
}
