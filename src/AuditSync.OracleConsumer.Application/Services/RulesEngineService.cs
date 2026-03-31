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
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║ TEST RESULTS - Audit Log: {auditLog.Id,-38} ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine($"Audit Details: Target={auditLog.Target}, DbUser={auditLog.DbUser}, Owner={auditLog.Owner}, Name={auditLog.Name}, Action={auditLog.Action}");
        Console.WriteLine("");

        if (rules.Count == 0)
        {
            Console.WriteLine("⚠ No rules available for evaluation\n");
            _logger.LogWarning("No rules available for evaluation");
            return null;
        }

        // Rules are already sorted by ORDER_POSITION in cache
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var matchResult = await EvaluateRuleAsync(rule, auditLog, i + 1, cancellationToken);

            if (matchResult != null && matchResult.Matched)
            {
                return matchResult; // Stop on first match
            }
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Result: No rules matched");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
        return null;
    }

    /// <summary>
    /// Evaluates a single rule against an audit log.
    /// Returns null if rule doesn't match.
    /// </summary>
    private async Task<RuleMatchResult?> EvaluateRuleAsync(
        Rule rule,
        AuditMessage auditLog,
        int ruleNumber,
        CancellationToken cancellationToken)
    {
        // Sort conditions by order (ascending)
        var sortedConditions = rule.Conditions.OrderBy(c => c.Order).ToList();

        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("Rule {RuleNumber}: {RuleName}", ruleNumber, rule.Name);
        _logger.LogInformation("");

        var allExtractions = new List<ExtractionResult>();
        var conditionResults = new List<(bool passed, string display)>();

        // Evaluate conditions with short-circuit logic
        for (int i = 0; i < sortedConditions.Count; i++)
        {
            var condition = sortedConditions[i];
            var conditionResult = EvaluateCondition(condition, auditLog, out var extractions, out var displayInfo);

            conditionResults.Add((conditionResult, displayInfo));

            if (!conditionResult)
            {
                // Condition failed - short circuit, rule doesn't match
                // Log the results so far
                LogRuleResult(rule, false, sortedConditions.Count, conditionResults, allExtractions, i + 1);
                return null;
            }

            // Condition passed, collect extractions if any
            if (extractions != null && extractions.Count > 0)
            {
                // Set source field for each extraction (Type is already set by JavaScript)
                foreach (var extraction in extractions)
                {
                    extraction.SourceField = condition.Field;
                }
                allExtractions.AddRange(extractions);
            }
        }

        // All conditions passed
        LogRuleResult(rule, true, sortedConditions.Count, conditionResults, allExtractions, sortedConditions.Count);

        return new RuleMatchResult
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            Matched = true,
            Actions = rule.Actions,
            Extractions = allExtractions
        };
    }

    private void LogRuleResult(
        Rule rule,
        bool matched,
        int totalConditions,
        List<(bool passed, string display)> conditionResults,
        List<ExtractionResult> extractions,
        int conditionsEvaluated)
    {
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine($"Rule {rule.OrderPosition + 1}: {rule.Name}");
        Console.WriteLine("");

        // Status line
        if (matched)
        {
            Console.WriteLine("Status: ✓ Matched");
        }
        else
        {
            Console.WriteLine("Status: ✗ Not Matched");
        }

        // Conditions summary
        int metCount = conditionResults.Count(c => c.passed);
        Console.WriteLine($"Conditions: {metCount} of {totalConditions} Met");
        Console.WriteLine("");

        // Condition details
        for (int i = 0; i < conditionResults.Count; i++)
        {
            var (passed, display) = conditionResults[i];
            Console.WriteLine($"• Condition {i + 1}: {(passed ? "Met" : "Not Met")}");
            Console.WriteLine($"  {display}");
            Console.WriteLine("");
        }

        // If there are uneval conditions (short-circuit), mention them
        if (!matched && conditionsEvaluated < totalConditions)
        {
            int remaining = totalConditions - conditionsEvaluated;
            Console.WriteLine($"  ({remaining} condition(s) not evaluated due to short-circuit)");
            Console.WriteLine("");
        }

        // JavaScript extraction results
        if (extractions.Count > 0)
        {
            Console.WriteLine("JavaScript Returned Object:");
            Console.WriteLine("[");
            foreach (var extraction in extractions)
            {
                Console.WriteLine("  {");
                Console.WriteLine($"    \"value\": \"{extraction.Value}\",");
                Console.WriteLine($"    \"type\": \"{extraction.Type}\",");
                Console.WriteLine($"    \"tags\": {(extraction.Tags != null && extraction.Tags.Count > 0 ? $"[\"{string.Join("\", \"", extraction.Tags)}\"]" : "[]")}");
                Console.WriteLine($"  }}{(extraction == extractions.Last() ? "" : ",")}");
            }
            Console.WriteLine("]");
            Console.WriteLine("");
        }

        // Actions
        if (matched)
        {
            var actions = new List<string>();
            if (rule.Actions.CreateCase)
            {
                actions.Add("Create Case");
            }
            if (rule.Actions.NotifyChannels != null && rule.Actions.NotifyChannels.Count > 0)
            {
                actions.Add($"Notify Channels: {string.Join(", ", rule.Actions.NotifyChannels)}");
            }

            if (actions.Count > 0)
            {
                Console.WriteLine($"Actions Triggered: {string.Join(", ", actions)}");
            }
            else
            {
                Console.WriteLine("Actions Triggered: None");
            }
        }

        Console.WriteLine("");
    }

    /// <summary>
    /// Evaluates a single condition against an audit log.
    /// Returns true if condition matches, false otherwise.
    /// </summary>
    private bool EvaluateCondition(
        RuleCondition condition,
        AuditMessage auditLog,
        out List<ExtractionResult>? extractions,
        out string displayInfo)
    {
        extractions = null;
        displayInfo = string.Empty;

        // DEBUG: Log the condition details
        _logger.LogDebug("Evaluating condition: Field={Field}, Operator={Operator}, Value={Value}, Extract={Extract}",
            condition.Field ?? "(null)",
            condition.Operator ?? "(null)",
            condition.Value?.ToString() ?? "(null)",
            condition.Extract);

        // Get the actual value from the audit log
        var actualValue = GetFieldValue(condition.Field, auditLog);

        _logger.LogDebug("Actual value from audit log: Field={Field}, Value={Value}",
            condition.Field ?? "(null)",
            actualValue ?? "(null)");

        // Evaluate operator
        var operatorKey = condition.Operator?.Trim().ToLower() ?? string.Empty;
        if (string.IsNullOrEmpty(operatorKey))
        {
            _logger.LogWarning("Condition has EMPTY operator! Field={Field}, Raw Operator={RawOperator}",
                condition.Field ?? "(null)",
                condition.Operator ?? "(null)");
            displayInfo = FormatConditionDisplay(condition.Field, "(empty)", condition.Value, actualValue);
            return false;
        }

        bool matches;
        try
        {
            matches = operatorKey switch
            {
                "equals" => EvaluateEquals(actualValue, condition.Value),
                "not_equals" => !EvaluateEquals(actualValue, condition.Value),
                "contains" => EvaluateContains(actualValue, condition.Value),
                "not_contains" => !EvaluateContains(actualValue, condition.Value),
                "starts_with" => EvaluateStartsWith(actualValue, condition.Value),
                "ends_with" => EvaluateEndsWith(actualValue, condition.Value),
                "regex" => EvaluateRegex(actualValue, condition.Value),
                "regex_in" => EvaluateRegexIn(actualValue, condition.Value),
                "in" => EvaluateIn(actualValue, condition.Value),
                "not_in" => !EvaluateIn(actualValue, condition.Value),
                "is_null" => EvaluateIsNull(actualValue),
                "is_not_null" => !EvaluateIsNull(actualValue),
                "gt" or "greater_than" => EvaluateGreaterThan(actualValue, condition.Value),
                "lt" or "less_than" => EvaluateLessThan(actualValue, condition.Value),
                "gte" or "greater_than_or_equal" => EvaluateGreaterThanOrEqual(actualValue, condition.Value),
                "lte" or "less_than_or_equal" => EvaluateLessThanOrEqual(actualValue, condition.Value),
                "between" => EvaluateBetween(actualValue, condition.Value),
                _ => throw new NotSupportedException($"Operator '{condition.Operator}' is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating operator '{Operator}' - Condition FAILS", operatorKey);
            displayInfo = FormatConditionDisplay(condition.Field, operatorKey, condition.Value, actualValue, $"Error: {ex.Message}");
            return false;
        }

        // Format display info
        displayInfo = FormatConditionDisplay(condition.Field, operatorKey, condition.Value, actualValue);

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
    /// Formats condition display information for logging.
    /// </summary>
    private string FormatConditionDisplay(string field, string operatorKey, object? expectedValue, string? actualValue, string? error = null)
    {
        var sb = new System.Text.StringBuilder();

        // Expected line
        sb.Append("Expected: ");
        sb.Append(field);
        sb.Append(" ");
        sb.Append(operatorKey);

        if (operatorKey != "is_null" && operatorKey != "is_not_null")
        {
            sb.Append(" ");
            sb.Append(FormatValueForDisplay(expectedValue));
        }

        sb.AppendLine();

        // Actual line
        sb.Append("  Actual: ");
        sb.Append(field);
        sb.Append(" = ");
        sb.Append(FormatValueForDisplay(actualValue));

        if (!string.IsNullOrEmpty(error))
        {
            sb.AppendLine();
            sb.Append("  ");
            sb.Append(error);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a value for display (truncates long strings, formats arrays).
    /// </summary>
    private string FormatValueForDisplay(object? value)
    {
        if (value == null)
        {
            return "(null)";
        }

        // Handle JSON arrays
        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                var items = jsonElement.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .ToList();
                return $"'{string.Join(",", items)}'";
            }
            return $"'{jsonElement.ToString()}'";
        }

        // Handle C# arrays/lists
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object>()
                .Select(o => o?.ToString() ?? string.Empty)
                .ToList();
            return $"'{string.Join(",", items)}'";
        }

        var stringValue = value.ToString() ?? string.Empty;

        // Truncate long strings
        if (stringValue.Length > 100)
        {
            return $"'{stringValue.Substring(0, 97)}...'";
        }

        return $"'{stringValue}'";
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

    // String Operators
    private bool EvaluateEquals(string? actual, object? expected)
    {
        if (actual == null && expected == null) return true;
        if (actual == null || expected == null) return false;

        return string.Equals(actual, expected.ToString(), StringComparison.Ordinal);
    }

    private bool EvaluateContains(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        return actual.Contains(expected.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    private bool EvaluateStartsWith(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        return actual.StartsWith(expected.ToString()!, StringComparison.Ordinal);
    }

    private bool EvaluateEndsWith(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        return actual.EndsWith(expected.ToString()!, StringComparison.Ordinal);
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

    private bool EvaluateRegexIn(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        List<string> patterns;

        // Check if expected is a JSON array
        if (expected is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            patterns = jsonElement.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        // Check if expected is a C# array/list
        else if (expected is System.Collections.IEnumerable enumerable && expected is not string)
        {
            patterns = enumerable.Cast<object>()
                .Select(o => o?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        else
        {
            _logger.LogWarning("regex_in operator requires an array value");
            return false;
        }

        // Test if actual matches ANY of the regex patterns
        foreach (var pattern in patterns)
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                if (regex.IsMatch(actual))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Regex error for pattern: {Pattern}", pattern);
                // Continue to next pattern
            }
        }

        return false;
    }

    // Array Operators
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

    // Null Operators
    private bool EvaluateIsNull(string? actual)
    {
        return string.IsNullOrWhiteSpace(actual);
    }

    // Numeric/DateTime Operators
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

    private bool EvaluateBetween(string? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (!double.TryParse(actual, out var actualNum))
        {
            return false;
        }

        // Expected should be an array with 2 values [min, max]
        List<double> range;

        if (expected is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            var arr = jsonElement.EnumerateArray().ToList();
            if (arr.Count != 2)
            {
                _logger.LogWarning("between operator requires exactly 2 values [min, max]");
                return false;
            }

            range = arr.Select(e =>
            {
                if (e.ValueKind == JsonValueKind.Number)
                {
                    return e.GetDouble();
                }
                else if (double.TryParse(e.GetString(), out var num))
                {
                    return num;
                }
                return 0;
            }).ToList();
        }
        else if (expected is System.Collections.IEnumerable enumerable && expected is not string)
        {
            var arr = enumerable.Cast<object>().ToList();
            if (arr.Count != 2)
            {
                _logger.LogWarning("between operator requires exactly 2 values [min, max]");
                return false;
            }

            range = arr.Select(o =>
            {
                if (double.TryParse(o?.ToString(), out var num))
                {
                    return num;
                }
                return 0;
            }).ToList();
        }
        else
        {
            _logger.LogWarning("between operator requires an array value [min, max]");
            return false;
        }

        var min = range[0];
        var max = range[1];

        return actualNum >= min && actualNum <= max;
    }

    #endregion
}
