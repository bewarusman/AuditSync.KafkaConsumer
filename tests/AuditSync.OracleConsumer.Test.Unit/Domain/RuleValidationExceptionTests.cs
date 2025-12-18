using AuditSync.OracleConsumer.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Domain;

public class RuleValidationExceptionTests
{
    [Fact]
    public void RuleValidationException_ShouldContainMessage()
    {
        // Arrange
        var message = "Required rule failed";

        // Act
        var exception = new RuleValidationException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void RuleValidationException_ShouldContainInnerException()
    {
        // Arrange
        var message = "Required rule failed";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new RuleValidationException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void RuleValidationException_ShouldBeThrowable()
    {
        // Arrange & Act
        Action act = () => throw new RuleValidationException("Test error");

        // Assert
        act.Should().Throw<RuleValidationException>()
            .WithMessage("Test error");
    }
}
