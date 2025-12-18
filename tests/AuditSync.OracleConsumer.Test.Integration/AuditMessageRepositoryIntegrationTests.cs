using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Integration;

[Trait("Category", "Integration")]
public class AuditMessageRepositoryIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task SaveAsync_ShouldInsertNewAuditMessage()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());
        var message = new AuditMessage
        {
            Id = "test-id-1",
            Target = "Production Oracle Database",
            SessionId = 123456,
            EntryId = 1,
            Statement = 1,
            DbUser = "TESTUSER",
            UserHost = "TESTHOST",
            Terminal = "TERMINAL1",
            OsUser = "osuser",
            Action = 3,
            ReturnCode = 0,
            Owner = "SCHEMA1",
            Name = "TABLE1",
            AuthPrivileges = "SELECT",
            AuthGrantee = "USER1",
            NewOwner = "",
            NewName = "",
            PrivilegeUsed = null,
            SqlText = "SELECT * FROM TABLE1",
            BindVariables = "#1(10):test",
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        // Act
        await repository.SaveAsync(message, 0, 12345);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ID, PROCESS_COUNTER, DB_USER, OWNER, NAME FROM audit_logs WHERE ID = :Id";
        command.Parameters.Add("Id", message.Id);

        using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("test-id-1");
        reader.GetInt32(1).Should().Be(1); // PROCESS_COUNTER should be 1
        reader.GetString(2).Should().Be("TESTUSER");
        reader.GetString(3).Should().Be("SCHEMA1");
        reader.GetString(4).Should().Be("TABLE1");
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdateExistingMessage_AndIncrementProcessCounter()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());
        var message = new AuditMessage
        {
            Id = "test-id-2",
            Target = "Production Oracle Database",
            SessionId = 123456,
            EntryId = 1,
            Statement = 1,
            DbUser = "USER1",
            UserHost = "HOST1",
            Terminal = "TERM1",
            OsUser = "osuser1",
            Action = 3,
            ReturnCode = 0,
            Owner = "SCHEMA1",
            Name = "TABLE1",
            AuthPrivileges = "",
            AuthGrantee = "",
            NewOwner = "",
            NewName = "",
            PrivilegeUsed = null,
            SqlText = "SELECT * FROM TABLE1",
            BindVariables = "",
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        // Act - Insert first time
        await repository.SaveAsync(message, 0, 100);

        // Modify message
        message.DbUser = "USER2";
        message.SqlText = "SELECT * FROM TABLE2";

        // Act - Save again (should update and increment counter)
        await repository.SaveAsync(message, 0, 100);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT PROCESS_COUNTER, DB_USER, TEXT FROM audit_logs WHERE ID = :Id";
        command.Parameters.Add("Id", message.Id);

        using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        reader.GetInt32(0).Should().Be(2); // PROCESS_COUNTER should be incremented to 2
        reader.GetString(1).Should().Be("USER2"); // Should be updated
        var text = reader.GetOracleClob(2).Value;
        text.Should().Be("SELECT * FROM TABLE2"); // Should be updated
    }

    [Fact]
    public async Task IsProcessedAsync_ShouldReturnTrue_WhenMessageExists()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());
        var message = new AuditMessage
        {
            Id = "test-id-3",
            Target = "Test",
            SessionId = 1,
            EntryId = 1,
            Statement = 1,
            DbUser = "USER",
            UserHost = "HOST",
            Terminal = "TERM",
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
            SqlText = "SELECT 1",
            BindVariables = "",
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        await repository.SaveAsync(message, 0, 200);

        // Act
        var result = await repository.IsProcessedAsync("test-id-3");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsProcessedAsync_ShouldReturnFalse_WhenMessageDoesNotExist()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());

        // Act
        var result = await repository.IsProcessedAsync("non-existent-id");

        // Assert
        result.Should().BeFalse();
    }
}
