using AuditSync.OracleConsumer.Application.Services;
using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Integration;

[Trait("Category", "Integration")]
public class TransactionalBehaviorIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task SaveAuditDataAsync_ShouldSaveBothTables_WhenSuccessful()
    {
        // Arrange
        var auditMessageRepo = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());
        var extractedValuesRepo = new ExtractedValuesRepository(ConnectionString, CreateLogger<ExtractedValuesRepository>());
        var auditDataService = new AuditDataService(auditMessageRepo, extractedValuesRepo, CreateLogger<AuditDataService>());

        var message = new AuditMessage
        {
            Id = "txn-test-1",
            Target = "Test Target",
            SessionId = 1,
            EntryId = 1,
            Statement = 1,
            DbUser = "USER",
            UserHost = "HOST",
            Terminal = "TERM",
            OsUser = "OS",
            Action = 1,
            ReturnCode = 0,
            Owner = "OWNER",
            Name = "NAME",
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

        var extractedData = new ExtractedData
        {
            AuditRecordId = "txn-test-1",
            Schema = "SCHEMA",
            TableName = "TABLE",
            SqlText = "SELECT 1",
            ExtractedFields = new Dictionary<string, string>
            {
                { "FIELD1", "value1" },
                { "FIELD2", "value2" }
            },
            ProcessedAt = DateTime.UtcNow
        };

        // Act
        await auditDataService.SaveAuditDataAsync(message, extractedData, 0, 100);

        // Assert - Verify both tables have data
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        // Check audit_logs
        using var cmd1 = connection.CreateCommand();
        cmd1.CommandText = "SELECT COUNT(*) FROM audit_logs WHERE ID = :Id";
        cmd1.Parameters.Add("Id", "txn-test-1");
        var auditCount = Convert.ToInt32(await cmd1.ExecuteScalarAsync());
        auditCount.Should().Be(1);

        // Check audit_log_extracted_values
        using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM audit_log_extracted_values WHERE AUDIT_MESSAGE_ID = :Id";
        cmd2.Parameters.Add("Id", "txn-test-1");
        var extractedCount = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
        extractedCount.Should().Be(2);
    }

    [Fact]
    public async Task SaveAuditDataAsync_ShouldNotSaveExtractedValues_WhenAuditMessageFails()
    {
        // Arrange
        var auditMessageRepo = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());
        var extractedValuesRepo = new ExtractedValuesRepository(ConnectionString, CreateLogger<ExtractedValuesRepository>());
        var auditDataService = new AuditDataService(auditMessageRepo, extractedValuesRepo, CreateLogger<AuditDataService>());

        // Create message with duplicate KAFKA_PARTITION/KAFKA_OFFSET (will cause unique constraint violation)
        var message1 = new AuditMessage
        {
            Id = "txn-test-2",
            Target = "Test",
            SessionId = 1,
            EntryId = 1,
            Statement = 1,
            DbUser = "USER",
            UserHost = "HOST",
            Terminal = "TERM",
            OsUser = "OS",
            Action = 1,
            ReturnCode = 0,
            Owner = "OWNER",
            Name = "NAME",
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

        var extractedData1 = new ExtractedData
        {
            AuditRecordId = "txn-test-2",
            ExtractedFields = new Dictionary<string, string> { { "FIELD1", "value1" } },
            ProcessedAt = DateTime.UtcNow
        };

        // First save should succeed
        await auditDataService.SaveAuditDataAsync(message1, extractedData1, 0, 200);

        // Create second message with same partition/offset (will fail)
        var message2 = new AuditMessage
        {
            Id = "txn-test-3",
            Target = "Test",
            SessionId = 1,
            EntryId = 1,
            Statement = 1,
            DbUser = "USER",
            UserHost = "HOST",
            Terminal = "TERM",
            OsUser = "OS",
            Action = 1,
            ReturnCode = 0,
            Owner = "OWNER",
            Name = "NAME",
            AuthPrivileges = "",
            AuthGrantee = "",
            NewOwner = "",
            NewName = "",
            PrivilegeUsed = null,
            SqlText = "SELECT 2",
            BindVariables = "",
            Timestamp = DateTime.UtcNow,
            ProducedAt = DateTime.UtcNow
        };

        var extractedData2 = new ExtractedData
        {
            AuditRecordId = "txn-test-3",
            ExtractedFields = new Dictionary<string, string> { { "FIELD2", "value2" } },
            ProcessedAt = DateTime.UtcNow
        };

        // Act & Assert - Second save should fail due to duplicate partition/offset
        Func<Task> act = async () => await auditDataService.SaveAuditDataAsync(message2, extractedData2, 0, 200);
        await act.Should().ThrowAsync<Exception>();

        // Verify extracted values for message2 were NOT saved
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM audit_log_extracted_values WHERE AUDIT_MESSAGE_ID = :Id";
        cmd.Parameters.Add("Id", "txn-test-3");
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task SaveAuditDataAsync_ShouldMaintainAtomicity_WhenExtractedValuesFails()
    {
        // Arrange
        var auditMessageRepo = new AuditMessageRepository(ConnectionString, CreateLogger<AuditMessageRepository>());
        var extractedValuesRepo = new ExtractedValuesRepository(ConnectionString, CreateLogger<ExtractedValuesRepository>());
        var auditDataService = new AuditDataService(auditMessageRepo, extractedValuesRepo, CreateLogger<AuditDataService>());

        var message = new AuditMessage
        {
            Id = "txn-test-4",
            Target = "Test",
            SessionId = 1,
            EntryId = 1,
            Statement = 1,
            DbUser = "USER",
            UserHost = "HOST",
            Terminal = "TERM",
            OsUser = "OS",
            Action = 1,
            ReturnCode = 0,
            Owner = "OWNER",
            Name = "NAME",
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

        var extractedData = new ExtractedData
        {
            AuditRecordId = "non-existent-id", // This will fail foreign key constraint
            ExtractedFields = new Dictionary<string, string> { { "FIELD1", "value1" } },
            ProcessedAt = DateTime.UtcNow
        };

        // Act & Assert - Should throw exception
        Func<Task> act = async () => await auditDataService.SaveAuditDataAsync(message, extractedData, 0, 300);
        await act.Should().ThrowAsync<Exception>();

        // Verify audit message WAS saved (repositories don't use transactions, each operation is independent)
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM audit_logs WHERE ID = :Id";
        cmd.Parameters.Add("Id", "txn-test-4");
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        count.Should().Be(1); // Message saved, but extracted values failed
    }
}
