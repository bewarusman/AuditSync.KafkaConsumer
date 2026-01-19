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
            SourceField = "sqlText".ToSourceFieldType(),
            RegexPattern = @"MSISDN=:(\w+)",
            RuleOrder = 1
        };

        // Assert
        rule.Id.Should().Be("rule-1");
        rule.TargetId.Should().Be("target-1");
        rule.TargetName.Should().Be("Production Oracle Database");
        rule.RuleName.Should().Be("MSISDN");
        rule.SourceField.Should().Be("sqlText".ToSourceFieldType());
        rule.RegexPattern.Should().Be(@"MSISDN=:(\w+)");
        rule.RuleOrder.Should().Be(1);
    }
}
