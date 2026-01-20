namespace AuditSync.OracleConsumer.Domain.Enums;

/// <summary>
/// Defines the types of conditions that can be used for tag evaluation.
/// </summary>
public enum TagConditionType
{
    /// <summary>
    /// Value starts with the condition value.
    /// Example: "964750445" matches values starting with "964750445"
    /// </summary>
    StartsWith = 0,

    /// <summary>
    /// Value ends with the condition value.
    /// Example: "777" matches values ending with "777"
    /// </summary>
    EndsWith = 1,

    /// <summary>
    /// Value exactly matches the condition value.
    /// Example: "9647507703030" matches only "9647507703030"
    /// </summary>
    Equals = 2,

    /// <summary>
    /// Value contains the condition value anywhere.
    /// Example: "750" matches any value containing "750"
    /// </summary>
    Contains = 3,

    /// <summary>
    /// Value matches a regex pattern.
    /// Example: ".*777$" matches values ending with "777"
    /// </summary>
    Regex = 4,

    /// <summary>
    /// Value is within a numeric range (inclusive).
    /// Example: "9647504450000-9647504459999" matches values in that range
    /// </summary>
    Range = 5
}
