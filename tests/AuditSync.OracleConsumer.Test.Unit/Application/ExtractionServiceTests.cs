using AuditSync.OracleConsumer.Application.Services;
using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Application;

public class ExtractionServiceTests
{
    private readonly Mock<ILogger<ExtractionService>> _loggerMock;
    private readonly ExtractionService _service;

    public ExtractionServiceTests()
    {
        _loggerMock = new Mock<ILogger<ExtractionService>>();
        _service = new ExtractionService(_loggerMock.Object);
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldReturnEmptyList_WhenNoRulesProvided()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var rules = new List<ExtractionRule>();

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractValue_WhenRegexMatches()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "SELECT * FROM users WHERE msisdn = '9647507703030'";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "msisdn",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].RuleId.Should().Be("rule-1");
        result[0].RuleName.Should().Be("msisdn");
        result[0].Value.Should().Be("9647507703030");
        result[0].SourceField.Should().Be("text");
        result[0].RegexPattern.Should().Be(@"msisdn\s*=\s*'(\d+)'");
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractMultipleValues_WhenMultipleRulesMatch()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "SELECT * FROM cdr WHERE msisdn = '9647507703030' AND imsi = '418576891839'";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "msisdn",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 1,
                IsActive = true
            },
            new ExtractionRule
            {
                Id = "rule-2",
                RuleName = "imsi",
                SourceField = "text",
                RegexPattern = @"imsi\s*=\s*'(\d+)'",
                RuleOrder = 2,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(2);
        result[0].RuleName.Should().Be("msisdn");
        result[0].Value.Should().Be("9647507703030");
        result[1].RuleName.Should().Be("imsi");
        result[1].Value.Should().Be("418576891839");
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldReturnEmptyList_WhenNoRulesMatch()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "SELECT * FROM users";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "msisdn",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractFromBindVariables_WhenSourceFieldIsBindVariables()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.BindVariables = "#1(12):9647507703030";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "msisdn",
                SourceField = "bindVariables",
                RegexPattern = @"#1\(\d+\):(\d+)",
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].Value.Should().Be("9647507703030");
        result[0].SourceField.Should().Be("bindVariables");
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldSkipRule_WhenSourceFieldIsEmpty()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "msisdn",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractFromDifferentSourceFields()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "SELECT * FROM users WHERE msisdn = '9647507703030'";
        auditMessage.Owner = "CDR_SCHEMA";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "msisdn",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 1,
                IsActive = true
            },
            new ExtractionRule
            {
                Id = "rule-2",
                RuleName = "schema",
                SourceField = "owner",
                RegexPattern = @"(CDR_\w+)",
                RuleOrder = 2,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(2);
        result[0].SourceField.Should().Be("text");
        result[0].Value.Should().Be("9647507703030");
        result[1].SourceField.Should().Be("owner");
        result[1].Value.Should().Be("CDR_SCHEMA");
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldHandleSpecialCharactersInSql()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "SELECT * FROM users WHERE email = 'test@example.com' AND phone = '964-750-770-3030'";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "email",
                SourceField = "text",
                RegexPattern = @"email\s*=\s*'([^']+@[^']+)'",
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].Value.Should().Be("test@example.com");
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldApplyRulesInOrder()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "SELECT * FROM users WHERE msisdn = '9647507703030'";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-3",
                RuleName = "rule3",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 3,
                IsActive = true
            },
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "rule1",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 1,
                IsActive = true
            },
            new ExtractionRule
            {
                Id = "rule-2",
                RuleName = "rule2",
                SourceField = "text",
                RegexPattern = @"msisdn\s*=\s*'(\d+)'",
                RuleOrder = 2,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(3);
        result[0].RuleId.Should().Be("rule-1"); // Order 1
        result[1].RuleId.Should().Be("rule-2"); // Order 2
        result[2].RuleId.Should().Be("rule-3"); // Order 3
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractDigitsOnly_WhenNoCapturingGroup()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "SELECT * FROM users WHERE msisdn = '9647507703030'";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "msisdn",
                SourceField = "text",
                RegexPattern = @"96475\d{8}", // No capturing group - matches only digits
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].RuleName.Should().Be("msisdn");
        result[0].Value.Should().Be("9647507703030"); // Only the matched digits
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldHandleNullSourceValue()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.PrivilegeUsed = null;

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "privilege",
                SourceField = "privilegeUsed",
                RegexPattern = @"(\w+)",
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRulesAsync_ShouldExtractFirstCapturingGroup()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        auditMessage.SqlText = "UPDATE users SET status = 5 WHERE msisdn = '9647507703030'";

        var rules = new List<ExtractionRule>
        {
            new ExtractionRule
            {
                Id = "rule-1",
                RuleName = "status_id",
                SourceField = "text",
                RegexPattern = @"status\s*=\s*(\d+)", // First capturing group
                RuleOrder = 1,
                IsActive = true
            }
        };

        // Act
        var result = await _service.ApplyRulesAsync(auditMessage, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].Value.Should().Be("5");
    }

    private AuditMessage CreateTestAuditMessage()
    {
        return new AuditMessage
        {
            Id = "test-id-1",
            Target = "Test Target",
            SessionId = 12345,
            EntryId = 1,
            Statement = 1,
            DbUser = "TEST_USER",
            UserHost = "localhost",
            Terminal = "terminal",
            OsUser = "osuser",
            Action = 3,
            ReturnCode = 0,
            Owner = "SCHEMA",
            Name = "TABLE",
            AuthPrivileges = "",
            AuthGrantee = "",
            NewOwner = "",
            NewName = "",
            PrivilegeUsed = null,
            Timestamp = DateTime.UtcNow,
            BindVariables = "",
            SqlText = "",
            ProducedAt = DateTime.UtcNow
        };
    }
}
