using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Models;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Microsoft.Extensions.Logging;

namespace AuditSync.OracleConsumer.Application.Services;

/// <summary>
/// Service for executing JavaScript extraction logic using Jint engine.
/// </summary>
public class JavaScriptExtractor
{
    private readonly ILogger<JavaScriptExtractor> _logger;
    private readonly TimeSpan _executionTimeout;

    public JavaScriptExtractor(ILogger<JavaScriptExtractor> logger, int timeoutSeconds = 5)
    {
        _logger = logger;
        _executionTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    /// <summary>
    /// Executes JavaScript extraction logic against a field value.
    /// </summary>
    /// <param name="extractionLogic">The JavaScript code to execute</param>
    /// <param name="fieldValue">The field value to process</param>
    /// <param name="auditLog">The complete audit log for context</param>
    /// <returns>List of extraction results</returns>
    public List<ExtractionResult> ExecuteExtraction(
        string extractionLogic,
        string fieldValue,
        AuditMessage auditLog)
    {
        if (string.IsNullOrEmpty(extractionLogic))
        {
            _logger.LogWarning("Extraction logic is empty");
            return new List<ExtractionResult>();
        }

        try
        {
            // Configure Jint engine with safety limits
            var engine = new Engine(options =>
            {
                options.TimeoutInterval(_executionTimeout);
                options.LimitRecursion(10);
                options.LimitMemory(10_000_000); // 10MB
            });

            // Set variables available to JavaScript
            engine.SetValue("value", fieldValue ?? string.Empty);

            // Convert audit log to JavaScript object
            var auditLogObj = new
            {
                id = auditLog.Id,
                target = auditLog.Target,
                sessionId = auditLog.SessionId,
                entryId = auditLog.EntryId,
                statement = auditLog.Statement,
                dbUser = auditLog.DbUser,
                userHost = auditLog.UserHost,
                terminal = auditLog.Terminal,
                osUser = auditLog.OsUser,
                action = auditLog.Action,
                returnCode = auditLog.ReturnCode,
                owner = auditLog.Owner,
                name = auditLog.Name,
                sqlText = auditLog.SqlText,
                bindVariables = auditLog.BindVariables,
                timestamp = auditLog.Timestamp,
                producedAt = auditLog.ProducedAt
            };

            engine.SetValue("auditLog", auditLogObj);

            // Provide webhook function (placeholder for future implementation)
            engine.SetValue("webhook", new Action<string, object>((name, payload) =>
            {
                _logger.LogDebug("Webhook called: {Name} (not implemented)", name);
                // Future: implement HTTP POST to webhook endpoint
            }));

            // Execute the JavaScript code
            var result = engine.Evaluate(extractionLogic);

            // Parse and return results
            return ParseJintResult(result);
        }
        catch (Jint.Runtime.JavaScriptException ex)
        {
            _logger.LogError(ex, "JavaScript execution error: {Message}", ex.Message);
            return new List<ExtractionResult>();
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "JavaScript execution timeout after {Timeout}ms", _executionTimeout.TotalMilliseconds);
            return new List<ExtractionResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JavaScript execution");
            return new List<ExtractionResult>();
        }
    }

    /// <summary>
    /// Parses Jint result into C# extraction results.
    /// Expected format: [{ value: "123", type: "MSISDN", tags: [] }]
    /// </summary>
    private List<ExtractionResult> ParseJintResult(JsValue result)
    {
        if (!result.IsArray())
        {
            _logger.LogWarning("Extraction must return an array, got: {Type}", result.Type);
            return new List<ExtractionResult>();
        }

        var array = result.AsArray();
        var extractions = new List<ExtractionResult>();

        for (int i = 0; i < array.Length; i++)
        {
            try
            {
                var item = array.Get(i.ToString());

                if (!item.IsObject())
                {
                    _logger.LogWarning("Extraction item at index {Index} is not an object", i);
                    continue;
                }

                var obj = item.AsObject();

                // Validate required fields
                if (!obj.HasProperty("value") ||
                    !obj.HasProperty("type") ||
                    !obj.HasProperty("tags"))
                {
                    _logger.LogWarning("Invalid extraction format at index {Index} - missing required fields (value, type, tags)", i);
                    continue;
                }

                var value = obj.Get("value");
                var type = obj.Get("type");
                var tags = obj.Get("tags");

                if (value.IsNull() || value.IsUndefined() || type.IsNull() || type.IsUndefined())
                {
                    _logger.LogWarning("Extraction at index {Index} has null value or type", i);
                    continue;
                }

                extractions.Add(new ExtractionResult
                {
                    Value = value.AsString(),
                    Type = type.AsString(),
                    Tags = ParseTags(tags)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing extraction at index {Index}", i);
                continue;
            }
        }

        return extractions;
    }

    /// <summary>
    /// Parses tags from Jint array value.
    /// </summary>
    private List<string> ParseTags(JsValue tagsValue)
    {
        if (tagsValue.IsNull() || tagsValue.IsUndefined())
        {
            return new List<string>();
        }

        if (!tagsValue.IsArray())
        {
            _logger.LogWarning("Tags must be an array");
            return new List<string>();
        }

        var tagsArray = tagsValue.AsArray();
        var tags = new List<string>();

        for (int i = 0; i < tagsArray.Length; i++)
        {
            try
            {
                var tagValue = tagsArray.Get(i.ToString());
                if (!tagValue.IsNull() && !tagValue.IsUndefined())
                {
                    tags.Add(tagValue.AsString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing tag at index {Index}", i);
            }
        }

        return tags;
    }
}
