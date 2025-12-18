namespace AuditSync.OracleConsumer.Domain.Exceptions;

/// <summary>
/// Exception thrown when a required extraction rule fails to match.
/// </summary>
public class RuleValidationException : Exception
{
    public RuleValidationException(string message) : base(message)
    {
    }

    public RuleValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
