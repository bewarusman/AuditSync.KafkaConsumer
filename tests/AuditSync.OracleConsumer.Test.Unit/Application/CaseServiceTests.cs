using AuditSync.OracleConsumer.Application.Services;
using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Application;

public class CaseServiceTests
{
    private readonly Mock<ICaseRepository> _caseRepositoryMock;
    private readonly Mock<ICaseExtractionRepository> _caseExtractionRepositoryMock;
    private readonly Mock<ILogger<CaseService>> _loggerMock;
    private readonly CaseService _service;

    public CaseServiceTests()
    {
        _caseRepositoryMock = new Mock<ICaseRepository>();
        _caseExtractionRepositoryMock = new Mock<ICaseExtractionRepository>();
        _loggerMock = new Mock<ILogger<CaseService>>();
        _service = new CaseService(
            _caseRepositoryMock.Object,
            _caseExtractionRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldReturnNull_WhenNoExtractedValues()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>();

        // Act
        var result = await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        result.Should().BeNull();
        _caseRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Case>()), Times.Never);
        _caseExtractionRepositoryMock.Verify(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()), Times.Never);
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldReturnNull_WhenExtractedValuesIsNull()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        List<ExtractedValue>? extractedValues = null;

        // Act
        var result = await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues!);

        // Assert
        result.Should().BeNull();
        _caseRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Case>()), Times.Never);
        _caseExtractionRepositoryMock.Verify(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()), Times.Never);
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldCreateCase_WhenExtractedValuesProvided()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .ReturnsAsync((Case c) => c.Id);

        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        _caseRepositoryMock.Verify(x => x.ExistsForAuditLogAsync(auditMessage.Id), Times.Once);
        _caseRepositoryMock.Verify(x => x.CreateAsync(It.Is<Case>(c =>
            c.AuditLogId == auditMessage.Id &&
            c.CaseStatus == "OPEN" &&
            c.Valid == null
        )), Times.Once);
        _caseExtractionRepositoryMock.Verify(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()), Times.Once);
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldReturnNull_WhenCaseAlreadyExists()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(true); // Case already exists

        // Act
        var result = await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        result.Should().BeNull();
        _caseRepositoryMock.Verify(x => x.ExistsForAuditLogAsync(auditMessage.Id), Times.Once);
        _caseRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Case>()), Times.Never);
        _caseExtractionRepositoryMock.Verify(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()), Times.Never);
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldCreateExtractionWithDenormalizedRuleInfo()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .ReturnsAsync((Case c) => c.Id);

        List<CaseExtraction>? capturedExtractions = null;
        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .Callback<List<CaseExtraction>>(extractions => capturedExtractions = extractions)
            .ReturnsAsync(1);

        // Act
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        capturedExtractions.Should().NotBeNull();
        capturedExtractions.Should().HaveCount(1);
        capturedExtractions![0].RuleId.Should().Be("rule-1");
        capturedExtractions[0].RuleName.Should().Be("msisdn");
        capturedExtractions[0].RegexPattern.Should().Be(@"\b964\d{10}\b");
        capturedExtractions[0].SourceField.Should().Be("text");
        capturedExtractions[0].FieldValue.Should().Be("9647507703030");
        capturedExtractions[0].AuditLogId.Should().Be(auditMessage.Id);
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldCreateMultipleExtractions_WhenMultipleValuesProvided()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            },
            new ExtractedValue
            {
                RuleId = "rule-2",
                RuleName = "imsi",
                RegexPattern = @"\b418\d{9}\b",
                SourceField = "text",
                Value = "418576891839"
            },
            new ExtractedValue
            {
                RuleId = "rule-3",
                RuleName = "status_id",
                RegexPattern = @"STATUS_ID\s*=\s*(\d+)",
                SourceField = "text",
                Value = "5"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .ReturnsAsync((Case c) => c.Id);

        List<CaseExtraction>? capturedExtractions = null;
        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .Callback<List<CaseExtraction>>(extractions => capturedExtractions = extractions)
            .ReturnsAsync(3);

        // Act
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        capturedExtractions.Should().NotBeNull();
        capturedExtractions.Should().HaveCount(3);
        capturedExtractions![0].RuleName.Should().Be("msisdn");
        capturedExtractions[1].RuleName.Should().Be("imsi");
        capturedExtractions[2].RuleName.Should().Be("status_id");
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldSetCaseStatusToOpen()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        Case? capturedCase = null;
        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .Callback<Case>(c => capturedCase = c)
            .ReturnsAsync((Case c) => c.Id);

        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .ReturnsAsync(1);

        // Act
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        capturedCase.Should().NotBeNull();
        capturedCase!.CaseStatus.Should().Be("OPEN");
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldSetValidToNull()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        Case? capturedCase = null;
        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .Callback<Case>(c => capturedCase = c)
            .ReturnsAsync((Case c) => c.Id);

        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .ReturnsAsync(1);

        // Act
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        capturedCase.Should().NotBeNull();
        capturedCase!.Valid.Should().BeNull();
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldSetTimestamps()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        Case? capturedCase = null;
        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .Callback<Case>(c => capturedCase = c)
            .ReturnsAsync((Case c) => c.Id);

        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .ReturnsAsync(1);

        // Act
        var beforeExecution = DateTime.UtcNow;
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);
        var afterExecution = DateTime.UtcNow;

        // Assert
        capturedCase.Should().NotBeNull();
        capturedCase!.CreatedAt.Should().BeOnOrAfter(beforeExecution).And.BeOnOrBefore(afterExecution);
        capturedCase.UpdatedAt.Should().BeOnOrAfter(beforeExecution).And.BeOnOrBefore(afterExecution);
        capturedCase.ResolvedAt.Should().BeNull();
        capturedCase.ResolvedBy.Should().BeNull();
        capturedCase.ResolutionNotes.Should().BeNull();
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldGenerateUniqueCaseId()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        Case? capturedCase = null;
        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .Callback<Case>(c => capturedCase = c)
            .ReturnsAsync((Case c) => c.Id);

        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .ReturnsAsync(1);

        // Act
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        capturedCase.Should().NotBeNull();
        capturedCase!.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(capturedCase.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldGenerateUniqueExtractionIds()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            },
            new ExtractedValue
            {
                RuleId = "rule-2",
                RuleName = "imsi",
                RegexPattern = @"\b418\d{9}\b",
                SourceField = "text",
                Value = "418576891839"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .ReturnsAsync((Case c) => c.Id);

        List<CaseExtraction>? capturedExtractions = null;
        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .Callback<List<CaseExtraction>>(extractions => capturedExtractions = extractions)
            .ReturnsAsync(2);

        // Act
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        capturedExtractions.Should().NotBeNull();
        capturedExtractions.Should().HaveCount(2);
        capturedExtractions![0].Id.Should().NotBeNullOrEmpty();
        capturedExtractions[1].Id.Should().NotBeNullOrEmpty();
        capturedExtractions[0].Id.Should().NotBe(capturedExtractions[1].Id);
        Guid.TryParse(capturedExtractions[0].Id, out _).Should().BeTrue();
        Guid.TryParse(capturedExtractions[1].Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateCaseWithExtractionsAsync_ShouldLinkExtractionsToCase()
    {
        // Arrange
        var auditMessage = CreateTestAuditMessage();
        var extractedValues = new List<ExtractedValue>
        {
            new ExtractedValue
            {
                RuleId = "rule-1",
                RuleName = "msisdn",
                RegexPattern = @"\b964\d{10}\b",
                SourceField = "text",
                Value = "9647507703030"
            }
        };

        _caseRepositoryMock
            .Setup(x => x.ExistsForAuditLogAsync(auditMessage.Id))
            .ReturnsAsync(false);

        string? createdCaseId = null;
        _caseRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Case>()))
            .Callback<Case>(c => createdCaseId = c.Id)
            .ReturnsAsync((Case c) => c.Id);

        List<CaseExtraction>? capturedExtractions = null;
        _caseExtractionRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<List<CaseExtraction>>()))
            .Callback<List<CaseExtraction>>(extractions => capturedExtractions = extractions)
            .ReturnsAsync(1);

        // Act
        await _service.CreateCaseWithExtractionsAsync(auditMessage, extractedValues);

        // Assert
        capturedExtractions.Should().NotBeNull();
        capturedExtractions.Should().HaveCount(1);
        capturedExtractions![0].CaseId.Should().Be(createdCaseId);
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
