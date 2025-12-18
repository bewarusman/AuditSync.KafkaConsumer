using AuditSync.OracleConsumer.Domain.Models;
using FluentAssertions;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Domain;

public class ExtractionRuleTests
{
    [Fact]
    public void ExtractionRule_ShouldHaveAllProperties()
    {
        // Arrange & Act
        var rule = new ExtractionRule
        {
            Id = "rule-1",
            TargetId = "target-1",
            TargetName = "Production Oracle Database",
            RuleName = "MSISDN",
            SourceField = "sqlText",
            RegexPattern = @"MSISDN=:(\w+)",
            IsRequired = false,
            IsActive = true,
            RuleOrder = 1
        };

        // Assert
        rule.Id.Should().Be("rule-1");
        rule.TargetId.Should().Be("target-1");
        rule.TargetName.Should().Be("Production Oracle Database");
        rule.RuleName.Should().Be("MSISDN");
        rule.SourceField.Should().Be("sqlText");
        rule.RegexPattern.Should().Be(@"MSISDN=:(\w+)");
        rule.IsRequired.Should().BeFalse();
        rule.IsActive.Should().BeTrue();
        rule.RuleOrder.Should().Be(1);
    }

    [Fact]
    public void ExtractionRule_IsRequired_DefaultsToFalse()
    {
        // Arrange & Act
        var rule = new ExtractionRule();

        // Assert
        rule.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ExtractionRule_IsActive_DefaultsToFalse()
    {
        // Arrange & Act
        var rule = new ExtractionRule();

        // Assert
        rule.IsActive.Should().BeFalse();
    }
}
