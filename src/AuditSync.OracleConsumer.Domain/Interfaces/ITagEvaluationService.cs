using AuditSync.OracleConsumer.Domain.Entities;

namespace AuditSync.OracleConsumer.Domain.Interfaces;

/// <summary>
/// Service for evaluating tag rules against extracted values.
/// </summary>
public interface ITagEvaluationService
{
    /// <summary>
    /// Evaluates all tag rules for a given extraction rule against an extracted value.
    /// Returns a list of tag names that should be applied.
    /// </summary>
    /// <param name="extractedValue">The extracted value to evaluate</param>
    /// <param name="tagRules">List of tag rules for this extraction rule</param>
    /// <returns>List of tag names that match the conditions</returns>
    Task<List<string>> EvaluateTagsAsync(string extractedValue, List<RuleTag> tagRules);
}
