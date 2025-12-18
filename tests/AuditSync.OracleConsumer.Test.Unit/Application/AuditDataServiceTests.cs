using AuditSync.OracleConsumer.Application.Services;
using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Application;

public class AuditDataServiceTests
{
    private readonly Mock<IAuditMessageRepository> _auditMessageRepositoryMock;
    private readonly Mock<IExtractedValuesRepository> _extractedValuesRepositoryMock;
    private readonly Mock<ILogger<AuditDataService>> _loggerMock;
    private readonly AuditDataService _auditDataService;

    public AuditDataServiceTests()
    {
        _auditMessageRepositoryMock = new Mock<IAuditMessageRepository>();
        _extractedValuesRepositoryMock = new Mock<IExtractedValuesRepository>();
        _loggerMock = new Mock<ILogger<AuditDataService>>();

        _auditDataService = new AuditDataService(
            _auditMessageRepositoryMock.Object,
            _extractedValuesRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SaveAuditDataAsync_ShouldSaveBothMessageAndExtractedValues()
    {
        // Arrange
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            SessionId = 123456,
            EntryId = 1,
            Owner = "SCHEMA",
            Name = "TABLE",
            SqlText = "SELECT * FROM TABLE"
        };

        var extractedData = new ExtractedData
        {
            AuditRecordId = "test-id",
            ExtractedFields = new Dictionary<string, string>
            {
                { "MSISDN", "1234567890" },
                { "STATUS_ID", "1" }
            }
        };

        int partition = 0;
        long offset = 12345;

        // Act
        await _auditDataService.SaveAuditDataAsync(message, extractedData, partition, offset);

        // Assert
        _auditMessageRepositoryMock.Verify(
            r => r.SaveAsync(message, partition, offset),
            Times.Once);

        _extractedValuesRepositoryMock.Verify(
            r => r.SaveExtractedValuesAsync("test-id", extractedData.ExtractedFields),
            Times.Once);
    }

    [Fact]
    public async Task SaveAuditDataAsync_ShouldSaveAuditMessageFirst()
    {
        // Arrange
        var message = new AuditMessage { Id = "test-id" };
        var extractedData = new ExtractedData
        {
            AuditRecordId = "test-id",
            ExtractedFields = new Dictionary<string, string>()
        };

        var callOrder = new List<string>();

        _auditMessageRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<AuditMessage>(), It.IsAny<int>(), It.IsAny<long>()))
            .Callback(() => callOrder.Add("AuditMessage"))
            .Returns(Task.CompletedTask);

        _extractedValuesRepositoryMock.Setup(r => r.SaveExtractedValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Callback(() => callOrder.Add("ExtractedValues"))
            .Returns(Task.CompletedTask);

        // Act
        await _auditDataService.SaveAuditDataAsync(message, extractedData, 0, 0);

        // Assert
        callOrder.Should().Equal("AuditMessage", "ExtractedValues");
    }

    [Fact]
    public async Task SaveAuditDataAsync_ShouldPropagateException_WhenAuditMessageSaveFails()
    {
        // Arrange
        var message = new AuditMessage { Id = "test-id" };
        var extractedData = new ExtractedData { AuditRecordId = "test-id", ExtractedFields = new Dictionary<string, string>() };

        _auditMessageRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<AuditMessage>(), It.IsAny<int>(), It.IsAny<long>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        Func<Task> act = async () => await _auditDataService.SaveAuditDataAsync(message, extractedData, 0, 0);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database error");

        // Verify extracted values were not saved
        _extractedValuesRepositoryMock.Verify(
            r => r.SaveExtractedValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveAuditDataAsync_ShouldHandleEmptyExtractedFields()
    {
        // Arrange
        var message = new AuditMessage { Id = "test-id" };
        var extractedData = new ExtractedData
        {
            AuditRecordId = "test-id",
            ExtractedFields = new Dictionary<string, string>()
        };

        // Act
        await _auditDataService.SaveAuditDataAsync(message, extractedData, 0, 0);

        // Assert
        _auditMessageRepositoryMock.Verify(
            r => r.SaveAsync(message, 0, 0),
            Times.Once);

        _extractedValuesRepositoryMock.Verify(
            r => r.SaveExtractedValuesAsync("test-id", It.IsAny<Dictionary<string, string>>()),
            Times.Once);
    }
}
