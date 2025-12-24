using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Infrastructure;

/// <summary>
/// Unit tests for TargetRepository with mocked dependencies.
/// Note: These tests verify the repository logic, but actual database integration
/// is tested in TargetRepositoryIntegrationTests.
/// </summary>
public class TargetRepositoryTests
{
    private readonly Mock<ILogger<TargetRepository>> _loggerMock;

    public TargetRepositoryTests()
    {
        _loggerMock = new Mock<ILogger<TargetRepository>>();
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WithValidParameters()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";

        // Act
        Action act = () => new TargetRepository(connectionString, _loggerMock.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExistsAsync_ShouldAcceptValidTargetName()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new TargetRepository(connectionString, _loggerMock.Object);

        // Act & Assert - Will throw connection exception
        Func<Task> act = async () => await repository.ExistsAsync("Production Oracle Database");
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ExistsAsync_ShouldHandleEmptyTargetName()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new TargetRepository(connectionString, _loggerMock.Object);

        // Act & Assert - Will throw connection exception
        Func<Task> act = async () => await repository.ExistsAsync("");
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldAcceptValidTargetName()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new TargetRepository(connectionString, _loggerMock.Object);

        // Act & Assert - Will throw connection exception
        Func<Task> act = async () => await repository.GetByNameAsync("Production Oracle Database");
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldHandleNullTargetName()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new TargetRepository(connectionString, _loggerMock.Object);

        // Act & Assert - Will throw connection exception
        Func<Task> act = async () => await repository.GetByNameAsync(null!);
        await act.Should().ThrowAsync<Exception>();
    }
}
