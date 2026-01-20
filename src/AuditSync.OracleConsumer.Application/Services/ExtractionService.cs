using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Service for applying extraction rules to audit messages.
/// Extracts values using regex patterns from specified source fields and applies tags based on tag rules.
/// </summary>
public class ExtractionService : IExtractionService
{
    private readonly ILogger<ExtractionService> _logger;
    private readonly IRuleTagRepository _ruleTagRepository;
    private readonly ITagEvaluationService _tagEvaluationService;

    public ExtractionService(
        ILogger<ExtractionService> logger,
        IRuleTagRepository ruleTagRepository,
        ITagEvaluationService tagEvaluationService)
    {
        _logger = logger;
        _ruleTagRepository = ruleTagRepository;
        _tagEvaluationService = tagEvaluationService;
    }

    public async Task<List<ExtractedValue>> ApplyRulesAsync(AuditMessage auditMessage, List<ExtractionRule> rules)
    {
        var extractedValues = new List<ExtractedValue>();

        if (rules == null || rules.Count == 0)
        {
            _logger.LogDebug("No rules provided for message {MessageId}", auditMessage.Id);
            return extractedValues;
        }

        // Load all tag rules for the extraction rules (batch load for efficiency)
        var ruleIds = rules.Select(r => r.Id).ToList();
        var tagRulesByRuleId = await _ruleTagRepository.GetActiveTagRulesByRuleIdsAsync(ruleIds);

        // Apply each rule
        foreach (var rule in rules.OrderBy(r => r.RuleOrder))
        {
            try
            {
                var sourceValue = GetSourceValue(auditMessage, rule.SourceField);

                var sourceFieldName = GetSourceFieldName(rule.SourceField);

                if (string.IsNullOrEmpty(sourceValue))
                {
                    _logger.LogDebug("Source field '{SourceField}' is empty for rule '{RuleName}'",
                        sourceFieldName, rule.RuleName);
                    continue;
                }

                // Apply regex pattern with timeout for safety - find ALL matches
                var regex = new Regex(rule.RegexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                var matches = regex.Matches(sourceValue);

                if (matches.Count > 0)
                {
                    // Get tag rules for this extraction rule
                    var tagRulesForRule = tagRulesByRuleId.ContainsKey(rule.Id)
                        ? tagRulesByRuleId[rule.Id]
                        : new List<RuleTag>();

                    // Extract ALL matches, not just the first one
                    foreach (Match match in matches)
                    {
                        if (match.Success)
                        {
                            // Use capturing group if available (Groups[1]), otherwise use the entire match
                            var capturedValue = match.Groups.Count > 1
                                ? match.Groups[1].Value
                                : match.Value;

                            // Evaluate tags for this extracted value
                            var appliedTags = await _tagEvaluationService.EvaluateTagsAsync(capturedValue, tagRulesForRule);

                            var extractedValue = new ExtractedValue
                            {
                                RuleId = rule.Id,
                                RuleName = rule.RuleName,
                                RegexPattern = rule.RegexPattern,
                                SourceField = sourceFieldName,
                                Value = capturedValue,
                                Tags = appliedTags
                            };

                            extractedValues.Add(extractedValue);

                            if (appliedTags.Count > 0)
                            {
                                _logger.LogDebug("Rule '{RuleName}' extracted value: {Value} with tags: {Tags}",
                                    rule.RuleName, capturedValue, string.Join(", ", appliedTags));
                            }
                            else
                            {
                                _logger.LogDebug("Rule '{RuleName}' extracted value: {Value}",
                                    rule.RuleName, capturedValue);
                            }
                        }
                    }

                    _logger.LogDebug("Rule '{RuleName}' extracted {MatchCount} value(s) from source field '{SourceField}'",
                        rule.RuleName, matches.Count, sourceFieldName);
                }
                else
                {
                    _logger.LogDebug("Rule '{RuleName}' did not match in source field '{SourceField}'",
                        rule.RuleName, sourceFieldName);
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.LogWarning(ex, "Regex timeout for rule '{RuleName}' on message {MessageId}",
                    rule.RuleName, auditMessage.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying rule '{RuleName}' to message {MessageId}",
                    rule.RuleName, auditMessage.Id);
            }
        }

        _logger.LogInformation("Extracted {Count} value(s) from message {MessageId}",
            extractedValues.Count, auditMessage.Id);

        return extractedValues;
    }

    private string? GetSourceValue(AuditMessage message, int sourceFieldType)
    {
        // Map source field to audit message property
        return (SourceFieldType)sourceFieldType switch
        {
            SourceFieldType.SqlText => message.SqlText,
            SourceFieldType.BindVariables => message.BindVariables,
            SourceFieldType.Owner => message.Owner,
            SourceFieldType.Name => message.Name,
            SourceFieldType.DbUser => message.DbUser,
            SourceFieldType.UserHost => message.UserHost,
            SourceFieldType.Terminal => message.Terminal,
            SourceFieldType.OsUser => message.OsUser,
            SourceFieldType.Target => message.Target,
            SourceFieldType.AuthPrivileges => message.AuthPrivileges,
            SourceFieldType.AuthGrantee => message.AuthGrantee,
            SourceFieldType.NewOwner => message.NewOwner,
            SourceFieldType.NewName => message.NewName,
            SourceFieldType.PrivilegeUsed => message.PrivilegeUsed,
            _ => null
        };
    }

    private string GetSourceFieldName(int sourceFieldType)
    {
        // Convert numeric field type to string name for denormalization
        return (SourceFieldType)sourceFieldType switch
        {
            SourceFieldType.SqlText => "SqlText",
            SourceFieldType.BindVariables => "BindVariables",
            SourceFieldType.Owner => "Owner",
            SourceFieldType.Name => "Name",
            SourceFieldType.DbUser => "DbUser",
            SourceFieldType.UserHost => "UserHost",
            SourceFieldType.Terminal => "Terminal",
            SourceFieldType.OsUser => "OsUser",
            SourceFieldType.Target => "Target",
            SourceFieldType.AuthPrivileges => "AuthPrivileges",
            SourceFieldType.AuthGrantee => "AuthGrantee",
            SourceFieldType.NewOwner => "NewOwner",
            SourceFieldType.NewName => "NewName",
            SourceFieldType.PrivilegeUsed => "PrivilegeUsed",
            _ => "Unknown"
        };
    }
}
