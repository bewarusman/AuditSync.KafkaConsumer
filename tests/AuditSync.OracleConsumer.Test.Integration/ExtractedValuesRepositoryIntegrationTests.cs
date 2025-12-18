using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Integration;

[Trait("Category", "Integration")]
public class ExtractedValuesRepositoryIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task SaveExtractedValuesAsync_ShouldInsertNewExtractedValues()
    {
        // Arrange
        var repository = new ExtractedValuesRepository(ConnectionString, CreateLogger<ExtractedValuesRepository>());

        // First, insert an audit message (required for foreign key)
        await InsertTestAuditMessageAsync("test-msg-1");

        var extractedFields = new Dictionary<string, string>
        {
            { "MSISDN", "9647515364803" },
            { "STATUS_ID", "1" },
            { "TABLE_NAME", "DEMO_CONTRACTS_TBL" }
        };

        // Act
        await repository.SaveExtractedValuesAsync("test-msg-1", extractedFields);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT FIELD_NAME, FIELD_VALUE FROM audit_log_extracted_values WHERE AUDIT_MESSAGE_ID = :MessageId ORDER BY FIELD_NAME";
        command.Parameters.Add("MessageId", "test-msg-1");

        var results = new Dictionary<string, string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results[reader.GetString(0)] = reader.GetString(1);
        }

        results.Should().HaveCount(3);
        results["MSISDN"].Should().Be("9647515364803");
        results["STATUS_ID"].Should().Be("1");
        results["TABLE_NAME"].Should().Be("DEMO_CONTRACTS_TBL");
    }

    [Fact]
    public async Task SaveExtractedValuesAsync_ShouldDeleteAndReInsert_OnDuplicateMessage()
    {
        // Arrange
        var repository = new ExtractedValuesRepository(ConnectionString, CreateLogger<ExtractedValuesRepository>());
        await InsertTestAuditMessageAsync("test-msg-2");

        var initialFields = new Dictionary<string, string>
        {
            { "MSISDN", "111111111" },
            { "STATUS_ID", "1" }
        };

        // Act - First insert
        await repository.SaveExtractedValuesAsync("test-msg-2", initialFields);

        // Modify extracted fields (simulating different extraction on duplicate message)
        var updatedFields = new Dictionary<string, string>
        {
            { "MSISDN", "222222222" },
            { "STATUS_ID", "2" },
            { "NEW_FIELD", "new_value" }
        };

        // Act - Second save (should delete old and insert new)
        await repository.SaveExtractedValuesAsync("test-msg-2", updatedFields);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT FIELD_NAME, FIELD_VALUE FROM audit_log_extracted_values WHERE AUDIT_MESSAGE_ID = :MessageId ORDER BY FIELD_NAME";
        command.Parameters.Add("MessageId", "test-msg-2");

        var results = new Dictionary<string, string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results[reader.GetString(0)] = reader.GetString(1);
        }

        results.Should().HaveCount(3);
        results["MSISDN"].Should().Be("222222222");
        results["STATUS_ID"].Should().Be("2");
        results["NEW_FIELD"].Should().Be("new_value");
    }

    [Fact]
    public async Task SaveExtractedValuesAsync_ShouldHandleEmptyFieldsDictionary()
    {
        // Arrange
        var repository = new ExtractedValuesRepository(ConnectionString, CreateLogger<ExtractedValuesRepository>());
        await InsertTestAuditMessageAsync("test-msg-3");

        var emptyFields = new Dictionary<string, string>();

        // Act
        await repository.SaveExtractedValuesAsync("test-msg-3", emptyFields);

        // Assert
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit_log_extracted_values WHERE AUDIT_MESSAGE_ID = :MessageId";
        command.Parameters.Add("MessageId", "test-msg-3");

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task SaveExtractedValuesAsync_ShouldRespectForeignKeyConstraint()
    {
        // Arrange
        var repository = new ExtractedValuesRepository(ConnectionString, CreateLogger<ExtractedValuesRepository>());

        var fields = new Dictionary<string, string>
        {
            { "FIELD1", "value1" }
        };

        // Act & Assert - Should throw exception for non-existent audit message
        Func<Task> act = async () => await repository.SaveExtractedValuesAsync("non-existent-id", fields);
        await act.Should().ThrowAsync<Exception>();
    }

    // Helper method to insert a test audit message
    private async Task InsertTestAuditMessageAsync(string messageId)
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO audit_logs (ID, TARGET, SESSION_ID, ENTRY_ID, STATEMENT, DB_USER, USER_HOST,
                TERMINAL, OS_USER, ACTION, RETURN_CODE, OWNER, NAME, AUTH_PRIVILEGES, AUTH_GRANTEE,
                NEW_OWNER, NEW_NAME, PRIVILEGE_USED, TEXT, BIND_VARIABLES, TIMESTAMP, PRODUCED_AT,
                KAFKA_PARTITION, KAFKA_OFFSET)
            VALUES (:Id, 'Test', 1, 1, 1, 'USER', 'HOST', 'TERM', 'OSUSER', 1, 0, 'OWNER', 'NAME',
                '', '', '', '', NULL, 'SELECT 1', '', SYSTIMESTAMP, SYSTIMESTAMP, 0, 0)";
        command.Parameters.Add("Id", messageId);
        await command.ExecuteNonQueryAsync();
    }
}
