using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Service for evaluating tag rules against extracted values.
/// Supports multiple condition types: StartsWith, EndsWith, Equals, Contains, Regex, Range.
/// </summary>
public class TagEvaluationService : ITagEvaluationService
{
    private readonly ILogger<TagEvaluationService> _logger;

    public TagEvaluationService(ILogger<TagEvaluationService> logger)
    {
        _logger = logger;
    }

    public async Task<List<string>> EvaluateTagsAsync(string extractedValue, List<RuleTag> tagRules)
    {
        var appliedTags = new List<string>();

        if (tagRules == null || tagRules.Count == 0 || string.IsNullOrEmpty(extractedValue))
        {
            return appliedTags;
        }

        // Process active tag rules ordered by priority (descending)
        foreach (var tagRule in tagRules.Where(t => t.IsActive).OrderByDescending(t => t.TagPriority))
        {
            try
            {
                bool matches = EvaluateCondition(extractedValue, tagRule);

                if (matches)
                {
                    appliedTags.Add(tagRule.TagName);
                    _logger.LogDebug("Tag '{TagName}' applied to value '{Value}' (Condition: {ConditionType} = {ConditionValue})",
                        tagRule.TagName, extractedValue, tagRule.ConditionType, tagRule.ConditionValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating tag rule '{TagId}' for value '{Value}'",
                    tagRule.Id, extractedValue);
            }
        }

        if (appliedTags.Count > 0)
        {
            _logger.LogInformation("Applied {Count} tag(s) to value '{Value}': {Tags}",
                appliedTags.Count, extractedValue, string.Join(", ", appliedTags));
        }

        return await Task.FromResult(appliedTags);
    }

    private bool EvaluateCondition(string value, RuleTag tagRule)
    {
        return tagRule.ConditionType switch
        {
            "StartsWith" => value.StartsWith(tagRule.ConditionValue, StringComparison.OrdinalIgnoreCase),

            "EndsWith" => value.EndsWith(tagRule.ConditionValue, StringComparison.OrdinalIgnoreCase),

            "Equals" => value.Equals(tagRule.ConditionValue, StringComparison.OrdinalIgnoreCase),

            "Contains" => value.Contains(tagRule.ConditionValue, StringComparison.OrdinalIgnoreCase),

            "Regex" => EvaluateRegexCondition(value, tagRule.ConditionValue),

            "Range" => EvaluateRangeCondition(value, tagRule.ConditionValue),

            _ => throw new ArgumentException($"Unsupported condition type: {tagRule.ConditionType}")
        };
    }

    private bool EvaluateRegexCondition(string value, string pattern)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "Regex timeout for pattern '{Pattern}' on value '{Value}'", pattern, value);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating regex pattern '{Pattern}'", pattern);
            return false;
        }
    }

    private bool EvaluateRangeCondition(string value, string rangeValue)
    {
        try
        {
            // Range format: "min-max" (e.g., "9647504450000-9647504459999")
            var parts = rangeValue.Split('-');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid range format: {RangeValue}. Expected 'min-max'", rangeValue);
                return false;
            }

            var min = parts[0].Trim();
            var max = parts[1].Trim();

            // Try numeric comparison first
            if (long.TryParse(value, out long numericValue) &&
                long.TryParse(min, out long numericMin) &&
                long.TryParse(max, out long numericMax))
            {
                return numericValue >= numericMin && numericValue <= numericMax;
            }

            // Fall back to string comparison (lexicographic)
            return string.Compare(value, min, StringComparison.Ordinal) >= 0 &&
                   string.Compare(value, max, StringComparison.Ordinal) <= 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating range condition '{RangeValue}' for value '{Value}'",
                rangeValue, value);
            return false;
        }
    }
}
