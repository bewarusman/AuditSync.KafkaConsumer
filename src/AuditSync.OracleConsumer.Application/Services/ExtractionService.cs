using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Service for applying extraction rules to audit messages.
/// Extracts values using regex patterns from specified source fields.
/// </summary>
public class ExtractionService : IExtractionService
{
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(ILogger<ExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ExtractedValue>> ApplyRulesAsync(AuditMessage auditMessage, List<ExtractionRule> rules)
    {
        var extractedValues = new List<ExtractedValue>();

        if (rules == null || rules.Count == 0)
        {
            _logger.LogDebug("No rules provided for message {MessageId}", auditMessage.Id);
            return extractedValues;
        }

        // Apply each rule
        foreach (var rule in rules.OrderBy(r => r.RuleOrder))
        {
            try
            {
                var sourceValue = GetSourceValue(auditMessage, rule.SourceField);

                if (string.IsNullOrEmpty(sourceValue))
                {
                    _logger.LogDebug("Source field '{SourceField}' is empty for rule '{RuleName}'",
                        rule.SourceField, rule.RuleName);
                    continue;
                }

                // Apply regex pattern with timeout for safety - find ALL matches
                var regex = new Regex(rule.RegexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                var matches = regex.Matches(sourceValue);

                if (matches.Count > 0)
                {
                    // Extract ALL matches, not just the first one
                    foreach (Match match in matches)
                    {
                        if (match.Success)
                        {
                            // Use capturing group if available (Groups[1]), otherwise use the entire match
                            var capturedValue = match.Groups.Count > 1
                                ? match.Groups[1].Value
                                : match.Value;

                            var extractedValue = new ExtractedValue
                            {
                                RuleId = rule.Id,
                                RuleName = rule.RuleName,
                                RegexPattern = rule.RegexPattern,
                                SourceField = rule.SourceField,
                                Value = capturedValue
                            };

                            extractedValues.Add(extractedValue);

                            _logger.LogDebug("Rule '{RuleName}' extracted value: {Value}",
                                rule.RuleName, capturedValue);
                        }
                    }

                    _logger.LogDebug("Rule '{RuleName}' extracted {MatchCount} value(s) from source field '{SourceField}'",
                        rule.RuleName, matches.Count, rule.SourceField);
                }
                else
                {
                    _logger.LogDebug("Rule '{RuleName}' did not match in source field '{SourceField}'",
                        rule.RuleName, rule.SourceField);
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

        return await Task.FromResult(extractedValues);
    }

    private string? GetSourceValue(AuditMessage message, string sourceField)
    {
        // Map source field to audit message property
        return sourceField.ToLower() switch
        {
            "text" or "sqltext" => message.SqlText,
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
