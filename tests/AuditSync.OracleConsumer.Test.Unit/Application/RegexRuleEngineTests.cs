using AuditSync.OracleConsumer.Application.Services;
using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Exceptions;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Application;

public class RegexRuleEngineTests
{
    private readonly Mock<IRuleRepository> _ruleRepositoryMock;
    private readonly Mock<ILogger<RegexRuleEngine>> _loggerMock;
    private readonly RegexRuleEngine _ruleEngine;

    public RegexRuleEngineTests()
    {
        _ruleRepositoryMock = new Mock<IRuleRepository>();
        _loggerMock = new Mock<ILogger<RegexRuleEngine>>();
        _ruleEngine = new RegexRuleEngine(_ruleRepositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractValueSuccessfully()
    {
        // Arrange
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            Owner = "TESTOWNER",
            Name = "TESTTABLE",
            SqlText = "SELECT * FROM TABLE WHERE MSISDN=:B1 AND STATUS_ID=1",
            BindVariables = "#1(13):9647515364803"
        };

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                RuleName = "MSISDN",
                SourceField = "bindVariables",
                RegexPattern = @"#1\(\d+\):(\d+)"
            },
            new ExtractionRule
            {
                RuleName = "STATUS_ID",
                SourceField = "sqlText",
                RegexPattern = @"STATUS_ID=(\d+)"
            }
        };

        _ruleRepositoryMock.Setup(r => r.GetRulesByTargetAsync("Production Oracle Database"))
            .ReturnsAsync(rules);

        // Act
        var result = await _ruleEngine.ApplyRulesAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.AuditRecordId.Should().Be("test-id");
        result.Schema.Should().Be("TESTOWNER");
        result.TableName.Should().Be("TESTTABLE");
        result.ExtractedFields.Should().HaveCount(2);
        result.ExtractedFields["MSISDN"].Should().Be("9647515364803");
        result.ExtractedFields["STATUS_ID"].Should().Be("1");
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldNotThrowException_WhenOptionalRuleFails()
    {
        // Arrange
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            Owner = "TESTOWNER",
            Name = "TESTTABLE",
            SqlText = "SELECT * FROM TABLE"
        };

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                RuleName = "OPTIONAL_FIELD",
                SourceField = "sqlText",
                RegexPattern = @"NONEXISTENT=(\w+)"
            }
        };

        _ruleRepositoryMock.Setup(r => r.GetRulesByTargetAsync("Production Oracle Database"))
            .ReturnsAsync(rules);

        // Act
        var result = await _ruleEngine.ApplyRulesAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.ExtractedFields.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldCacheRules_OnSecondCall()
    {
        // Arrange
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            Owner = "TESTOWNER",
            Name = "TESTTABLE",
            SqlText = "SELECT * FROM TABLE WHERE MSISDN=:B1"
        };

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                RuleName = "MSISDN",
                SourceField = "sqlText",
                RegexPattern = @"MSISDN=:(\w+)"
            }
        };

        _ruleRepositoryMock.Setup(r => r.GetRulesByTargetAsync("Production Oracle Database"))
            .ReturnsAsync(rules);

        // Act - First call
        await _ruleEngine.ApplyRulesAsync(message);

        // Act - Second call (should use cache)
        await _ruleEngine.ApplyRulesAsync(message);

        // Assert - Repository should be called only once (first call loads, second uses cache)
        _ruleRepositoryMock.Verify(r => r.GetRulesByTargetAsync("Production Oracle Database"), Times.Once);
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldHandleEmptyRulesList()
    {
        // Arrange
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            Owner = "TESTOWNER",
            Name = "TESTTABLE",
            SqlText = "SELECT * FROM TABLE"
        };

        _ruleRepositoryMock.Setup(r => r.GetRulesByTargetAsync("Production Oracle Database"))
            .ReturnsAsync(new List<ExtractionRule>());

        // Act
        var result = await _ruleEngine.ApplyRulesAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.ExtractedFields.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractFromAllSupportedFields()
    {
        // Arrange
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            Owner = "SCHEMA1",
            Name = "TABLE1",
            SqlText = "SELECT * FROM TABLE1",
            BindVariables = "#1(10):testvalue",
            DbUser = "USER1",
            UserHost = "HOST1",
            Terminal = "TERM1",
            OsUser = "osuser1"
        };

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule { RuleName = "OWNER", SourceField = "owner", RegexPattern = @"^(\w+)$" },
            new ExtractionRule { RuleName = "NAME", SourceField = "name", RegexPattern = @"^(\w+)$" },
            new ExtractionRule { RuleName = "TEXT_MATCH", SourceField = "sqlText", RegexPattern = @"FROM (\w+)" },
            new ExtractionRule { RuleName = "BIND_VALUE", SourceField = "bindVariables", RegexPattern = @":(\w+)" },
            new ExtractionRule { RuleName = "DB_USER", SourceField = "dbUser", RegexPattern = @"^(\w+)$" }
        };

        _ruleRepositoryMock.Setup(r => r.GetRulesByTargetAsync("Production Oracle Database"))
            .ReturnsAsync(rules);

        // Act
        var result = await _ruleEngine.ApplyRulesAsync(message);

        // Assert
        result.ExtractedFields.Should().Contain("OWNER", "SCHEMA1");
        result.ExtractedFields.Should().Contain("NAME", "TABLE1");
        result.ExtractedFields.Should().Contain("TEXT_MATCH", "TABLE1");
        result.ExtractedFields.Should().Contain("BIND_VALUE", "testvalue");
        result.ExtractedFields.Should().Contain("DB_USER", "USER1");
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldHandleNullSourceField()
    {
        // Arrange
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            Owner = "SCHEMA1",
            Name = "TABLE1",
            SqlText = "SELECT * FROM TABLE1",
            PrivilegeUsed = null
        };

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                RuleName = "PRIVILEGE",
                SourceField = "privilegeUsed",
                RegexPattern = @"(\w+)"
            }
        };

        _ruleRepositoryMock.Setup(r => r.GetRulesByTargetAsync("Production Oracle Database"))
            .ReturnsAsync(rules);

        // Act
        var result = await _ruleEngine.ApplyRulesAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.ExtractedFields.Should().NotContainKey("PRIVILEGE");
    }
}
