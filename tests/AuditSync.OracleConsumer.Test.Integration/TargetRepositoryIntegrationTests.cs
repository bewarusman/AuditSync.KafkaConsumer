using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Integration;

[Trait("Category", "Integration")]
public class TargetRepositoryIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenTargetExists()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());
        await InsertTestTargetAsync();

        // Act
        var exists = await repository.ExistsAsync("Production Oracle Database");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenTargetDoesNotExist()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());
        await InsertTestTargetAsync();

        // Act
        var exists = await repository.ExistsAsync("Non-Existent Target");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenNoTargetsExist()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());

        // Act
        var exists = await repository.ExistsAsync("Any Target");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnTarget_WhenTargetExists()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());
        await InsertTestTargetAsync();

        // Act
        var target = await repository.GetByNameAsync("Production Oracle Database");

        // Assert
        target.Should().NotBeNull();
        target!.Id.Should().Be("target-1");
        target.Name.Should().Be("Production Oracle Database");
        target.Description.Should().Be("Main production database");
        target.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        target.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnNull_WhenTargetDoesNotExist()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());
        await InsertTestTargetAsync();

        // Act
        var target = await repository.GetByNameAsync("Non-Existent Target");

        // Assert
        target.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnTargetWithNullDescription_WhenDescriptionIsNull()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());
        await InsertTargetWithNullDescriptionAsync();

        // Act
        var target = await repository.GetByNameAsync("Target Without Description");

        // Assert
        target.Should().NotBeNull();
        target!.Id.Should().Be("target-2");
        target.Name.Should().Be("Target Without Description");
        target.Description.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ShouldBeCaseSensitive()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());
        await InsertTestTargetAsync();

        // Act
        var exists = await repository.ExistsAsync("production oracle database"); // lowercase

        // Assert - Oracle is case-sensitive for string comparisons
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnCorrectTarget_WhenMultipleTargetsExist()
    {
        // Arrange
        var repository = new TargetRepository(ConnectionString, CreateLogger<TargetRepository>());
        await InsertMultipleTargetsAsync();

        // Act
        var target = await repository.GetByNameAsync("DWH Database");

        // Assert
        target.Should().NotBeNull();
        target!.Id.Should().Be("target-dwh");
        target.Name.Should().Be("DWH Database");
        target.Description.Should().Be("Data Warehouse");
    }

    // Helper methods to insert test data

    private async Task InsertTestTargetAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO targets (ID, NAME, DESCRIPTION)
            VALUES ('target-1', 'Production Oracle Database', 'Main production database')";
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertTargetWithNullDescriptionAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO targets (ID, NAME, DESCRIPTION)
            VALUES ('target-2', 'Target Without Description', NULL)";
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertMultipleTargetsAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        var targets = new[]
        {
            ("target-prod", "Production Oracle Database", "Main production database"),
            ("target-dwh", "DWH Database", "Data Warehouse"),
            ("target-test", "Test Database", "Testing environment")
        };

        foreach (var (id, name, description) in targets)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO targets (ID, NAME, DESCRIPTION)
                VALUES (:Id, :Name, :Description)";
            command.Parameters.Add("Id", id);
            command.Parameters.Add("Name", name);
            command.Parameters.Add("Description", description);
            await command.ExecuteNonQueryAsync();
        }
    }
}
