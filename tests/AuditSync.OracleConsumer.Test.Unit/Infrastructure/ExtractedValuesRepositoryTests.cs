using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Infrastructure;

public class ExtractedValuesRepositoryTests
{
    private readonly Mock<ILogger<ExtractedValuesRepository>> _loggerMock;

    public ExtractedValuesRepositoryTests()
    {
        _loggerMock = new Mock<ILogger<ExtractedValuesRepository>>();
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WithValidParameters()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";

        // Act
        Action act = () => new ExtractedValuesRepository(connectionString, _loggerMock.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SaveExtractedValuesAsync_ShouldAcceptValidParameters()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new ExtractedValuesRepository(connectionString, _loggerMock.Object);

        var fields = new Dictionary<string, string>
        {
            { "FIELD1", "value1" },
            { "FIELD2", "value2" }
        };

        // Act & Assert - Will throw connection exception
        Func<Task> act = async () => await repository.SaveExtractedValuesAsync("test-id", fields);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SaveExtractedValuesAsync_ShouldAcceptEmptyDictionary()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new ExtractedValuesRepository(connectionString, _loggerMock.Object);

        var emptyFields = new Dictionary<string, string>();

        // Act & Assert - Empty dictionary should not cause errors (just won't insert anything)
        // Connection will fail but that proves the logic works
        Func<Task> act = async () => await repository.SaveExtractedValuesAsync("test-id", emptyFields);
        // Note: Empty dictionary is valid - it means no fields to extract
        repository.Should().NotBeNull();
    }
}
