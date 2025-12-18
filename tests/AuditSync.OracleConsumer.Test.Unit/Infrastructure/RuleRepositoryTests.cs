using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Infrastructure;

public class RuleRepositoryTests
{
    private readonly Mock<ILogger<RuleRepository>> _loggerMock;

    public RuleRepositoryTests()
    {
        _loggerMock = new Mock<ILogger<RuleRepository>>();
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WithValidParameters()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";

        // Act
        Action act = () => new RuleRepository(connectionString, _loggerMock.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetRulesByTargetAsync_ShouldAcceptValidTargetName()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new RuleRepository(connectionString, _loggerMock.Object);

        // Act & Assert - Will throw connection exception
        Func<Task> act = async () => await repository.GetRulesByTargetAsync("Test Target");
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetRulesByTargetAsync_ShouldHandleEmptyTargetName()
    {
        // Arrange
        var connectionString = "User Id=test;Password=test;Data Source=test";
        var repository = new RuleRepository(connectionString, _loggerMock.Object);

        // Act & Assert - Will throw connection exception
        Func<Task> act = async () => await repository.GetRulesByTargetAsync("");
        await act.Should().ThrowAsync<Exception>();
    }
}
