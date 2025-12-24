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

    // CLOB Handling Integration Tests

    [Fact]
    public async Task SaveAsync_ShouldStoreLargeSqlText()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());

        // Create a large SQL text (10KB) - exceeds VARCHAR2 limit of 4000 chars
        var largeSqlText = new string('X', 10000);
        var message = new AuditMessage
        {
            Id = "clob-test-1",
            Target = "DWH",
            SessionId = 12345,
            EntryId = 1,
            Statement = 1,
            DbUser = "USER",
            UserHost = "HOST",
            Terminal = "TERM",
            OsUser = "osuser",
            Action = 3,
            ReturnCode = 0,
            Owner = "OCDM_SYS",
            Name = "DWR_DLR",
            AuthPrivileges = "",
            AuthGrantee = "",
            NewOwner = "",
            NewName = "",
            PrivilegeUsed = null,
            SqlText = largeSqlText,
            BindVariables = "",
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        // Act
        await repository.SaveAsync(message, 0, 500);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TEXT FROM audit_logs WHERE ID = :Id";
        command.Parameters.Add("Id", message.Id);

        using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        var retrievedText = reader.GetOracleClob(0).Value;
        retrievedText.Should().HaveLength(10000);
        retrievedText.Should().Be(largeSqlText);
    }

    [Fact]
    public async Task SaveAsync_ShouldStoreVeryLargeSqlText()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());

        // Create a very large SQL text (50KB)
        var veryLargeSqlText = new string('S', 50000);
        var message = new AuditMessage
        {
            Id = "clob-test-2",
            Target = "DWH",
            SessionId = 157166767,
            EntryId = 22,
            Statement = 7,
            DbUser = "KOREK_ODIM",
            UserHost = "edwhdbadm01.korektel.com",
            Terminal = "unknown",
            OsUser = "ocdmuser",
            Action = 3,
            ReturnCode = 0,
            Owner = "OCDM_SYS",
            Name = "DWB_KOREK_SBRP_CELL_RGN",
            AuthPrivileges = "",
            AuthGrantee = "",
            NewOwner = "",
            NewName = "",
            PrivilegeUsed = null,
            SqlText = veryLargeSqlText,
            BindVariables = "",
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        // Act
        await repository.SaveAsync(message, 0, 501);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TEXT FROM audit_logs WHERE ID = :Id";
        command.Parameters.Add("Id", message.Id);

        using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        var retrievedText = reader.GetOracleClob(0).Value;
        retrievedText.Should().HaveLength(50000);
        retrievedText.Should().Be(veryLargeSqlText);
    }

    [Fact]
    public async Task SaveAsync_ShouldStoreLargeBindVariables()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());

        // Create large bind variables (8KB)
        var largeBindVars = new string('B', 8000);
        var message = new AuditMessage
        {
            Id = "clob-test-3",
            Target = "DWH",
            SessionId = 12345,
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
            BindVariables = largeBindVars,
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        // Act
        await repository.SaveAsync(message, 0, 502);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT BIND_VARIABLES FROM audit_logs WHERE ID = :Id";
        command.Parameters.Add("Id", message.Id);

        using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        var retrievedBindVars = reader.GetOracleClob(0).Value;
        retrievedBindVars.Should().HaveLength(8000);
        retrievedBindVars.Should().Be(largeBindVars);
    }

    [Fact]
    public async Task SaveAsync_ShouldStoreBothLargeClobFields()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());

        var largeSqlText = new string('Q', 15000);
        var largeBindVars = new string('P', 12000);

        var message = new AuditMessage
        {
            Id = "clob-test-4",
            Target = "DWH",
            SessionId = 12345,
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
            SqlText = largeSqlText,
            BindVariables = largeBindVars,
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        // Act
        await repository.SaveAsync(message, 0, 503);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TEXT, BIND_VARIABLES FROM audit_logs WHERE ID = :Id";
        command.Parameters.Add("Id", message.Id);

        using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        var retrievedText = reader.GetOracleClob(0).Value;
        var retrievedBindVars = reader.GetOracleClob(1).Value;

        retrievedText.Should().HaveLength(15000);
        retrievedText.Should().Be(largeSqlText);
        retrievedBindVars.Should().HaveLength(12000);
        retrievedBindVars.Should().Be(largeBindVars);
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdateLargeClobFields()
    {
        // Arrange
        var repository = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());

        var initialSqlText = new string('A', 5000);
        var updatedSqlText = new string('Z', 7000);

        var message = new AuditMessage
        {
            Id = "clob-test-5",
            Target = "DWH",
            SessionId = 12345,
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
            SqlText = initialSqlText,
            BindVariables = "",
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        // Act - Insert first
        await repository.SaveAsync(message, 0, 504);

        // Update with different large text
        message.SqlText = updatedSqlText;
        await repository.SaveAsync(message, 0, 504);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TEXT, PROCESS_COUNTER FROM audit_logs WHERE ID = :Id";
        command.Parameters.Add("Id", message.Id);

        using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        var retrievedText = reader.GetOracleClob(0).Value;
        var processCounter = reader.GetInt32(1);

        retrievedText.Should().HaveLength(7000);
        retrievedText.Should().Be(updatedSqlText);
        processCounter.Should().Be(2); // Should be incremented
    }
}
