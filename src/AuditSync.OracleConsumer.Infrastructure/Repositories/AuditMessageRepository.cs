using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for storing audit messages in Oracle database.
/// Uses MERGE (upsert) logic to prevent duplicates.
/// </summary>
public class AuditMessageRepository : IAuditMessageRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AuditMessageRepository> _logger;

    public AuditMessageRepository(string connectionString, ILogger<AuditMessageRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SaveAsync(AuditMessage message, int partition, long offset)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            // Check if record exists
            var checkSql = "SELECT COUNT(1) FROM audit_logs WHERE ID = :Id";
            var exists = await connection.ExecuteScalarAsync<int>(checkSql, new { Id = message.Id }) > 0;

            if (exists)
            {
                // UPDATE existing record
                var updateSql = @"
                    UPDATE audit_logs SET
                        TARGET = :Target,
                        SESSION_ID = :SessionId,
                        ENTRY_ID = :EntryId,
                        STATEMENT = :Statement,
                        DB_USER = :DbUser,
                        USER_HOST = :UserHost,
                        TERMINAL = :Terminal,
                        OS_USER = :OsUser,
                        ACTION = :Action,
                        RETURN_CODE = :ReturnCode,
                        OWNER = :Owner,
                        NAME = :Name,
                        AUTH_PRIVILEGES = :AuthPrivileges,
                        AUTH_GRANTEE = :AuthGrantee,
                        NEW_OWNER = :NewOwner,
                        NEW_NAME = :NewName,
                        PRIVILEGE_USED = :PrivilegeUsed,
                        TEXT = :Text,
                        BIND_VARIABLES = :BindVariables,
                        TIMESTAMP = :Timestamp,
                        PRODUCED_AT = :ProducedAt,
                        KAFKA_PARTITION = :KafkaPartition,
                        KAFKA_OFFSET = :KafkaOffset,
                        PROCESS_COUNTER = PROCESS_COUNTER + 1,
                        CONSUMED_AT = SYSTIMESTAMP
                    WHERE ID = :Id";

                // Use OracleCommand with explicit CLOB parameters for large text fields
                using var command = connection.CreateCommand();
                command.CommandText = updateSql;

                // Add parameters with explicit CLOB type for TEXT and BIND_VARIABLES
                command.Parameters.Add("Target", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Target) ? DBNull.Value : message.Target;
                command.Parameters.Add("SessionId", OracleDbType.Int64).Value = message.SessionId;
                command.Parameters.Add("EntryId", OracleDbType.Int32).Value = message.EntryId;
                command.Parameters.Add("Statement", OracleDbType.Int32).Value = message.Statement;
                command.Parameters.Add("DbUser", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.DbUser) ? DBNull.Value : message.DbUser;
                command.Parameters.Add("UserHost", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.UserHost) ? DBNull.Value : message.UserHost;
                command.Parameters.Add("Terminal", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Terminal) ? DBNull.Value : message.Terminal;
                command.Parameters.Add("OsUser", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.OsUser) ? DBNull.Value : message.OsUser;
                command.Parameters.Add("Action", OracleDbType.Int32).Value = message.Action;
                command.Parameters.Add("ReturnCode", OracleDbType.Int32).Value = message.ReturnCode;
                command.Parameters.Add("Owner", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Owner) ? DBNull.Value : message.Owner;
                command.Parameters.Add("Name", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Name) ? DBNull.Value : message.Name;
                command.Parameters.Add("AuthPrivileges", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.AuthPrivileges) ? DBNull.Value : message.AuthPrivileges;
                command.Parameters.Add("AuthGrantee", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.AuthGrantee) ? DBNull.Value : message.AuthGrantee;
                command.Parameters.Add("NewOwner", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.NewOwner) ? DBNull.Value : message.NewOwner;
                command.Parameters.Add("NewName", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.NewName) ? DBNull.Value : message.NewName;
                command.Parameters.Add("PrivilegeUsed", OracleDbType.Varchar2).Value = message.PrivilegeUsed ?? (object)DBNull.Value;

                // CLOB parameters for large text fields
                command.Parameters.Add("Text", OracleDbType.Clob).Value = string.IsNullOrEmpty(message.SqlText) ? DBNull.Value : message.SqlText;
                command.Parameters.Add("BindVariables", OracleDbType.Clob).Value = string.IsNullOrEmpty(message.BindVariables) ? DBNull.Value : message.BindVariables;

                command.Parameters.Add("Timestamp", OracleDbType.TimeStamp).Value = message.Timestamp;
                command.Parameters.Add("ProducedAt", OracleDbType.TimeStamp).Value = message.ProducedAt;
                command.Parameters.Add("KafkaPartition", OracleDbType.Int32).Value = partition;
                command.Parameters.Add("KafkaOffset", OracleDbType.Int64).Value = offset;
                command.Parameters.Add("Id", OracleDbType.Varchar2).Value = message.Id;

                await command.ExecuteNonQueryAsync();

                _logger.LogDebug("Updated audit message {MessageId} (partition: {Partition}, offset: {Offset})",
                    message.Id, partition, offset);
            }
            else
            {
                // INSERT new record
                var insertSql = @"
                    INSERT INTO audit_logs (ID, TARGET, SESSION_ID, ENTRY_ID, STATEMENT, DB_USER, USER_HOST, TERMINAL, OS_USER,
                            ACTION, RETURN_CODE, OWNER, NAME, AUTH_PRIVILEGES, AUTH_GRANTEE, NEW_OWNER, NEW_NAME,
                            PRIVILEGE_USED, TEXT, BIND_VARIABLES, TIMESTAMP, PRODUCED_AT, KAFKA_PARTITION, KAFKA_OFFSET,
                            PROCESS_COUNTER, PROCESSED_AT, CONSUMED_AT)
                    VALUES (:Id, :Target, :SessionId, :EntryId, :Statement, :DbUser, :UserHost, :Terminal, :OsUser,
                            :Action, :ReturnCode, :Owner, :Name, :AuthPrivileges, :AuthGrantee, :NewOwner, :NewName,
                            :PrivilegeUsed, :Text, :BindVariables, :Timestamp, :ProducedAt, :KafkaPartition, :KafkaOffset,
                            1, SYSTIMESTAMP, SYSTIMESTAMP)";

                // Use OracleCommand with explicit CLOB parameters for large text fields
                using var command = connection.CreateCommand();
                command.CommandText = insertSql;

                // Add parameters with explicit CLOB type for TEXT and BIND_VARIABLES
                command.Parameters.Add("Id", OracleDbType.Varchar2).Value = message.Id;
                command.Parameters.Add("Target", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Target) ? DBNull.Value : message.Target;
                command.Parameters.Add("SessionId", OracleDbType.Int64).Value = message.SessionId;
                command.Parameters.Add("EntryId", OracleDbType.Int32).Value = message.EntryId;
                command.Parameters.Add("Statement", OracleDbType.Int32).Value = message.Statement;
                command.Parameters.Add("DbUser", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.DbUser) ? DBNull.Value : message.DbUser;
                command.Parameters.Add("UserHost", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.UserHost) ? DBNull.Value : message.UserHost;
                command.Parameters.Add("Terminal", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Terminal) ? DBNull.Value : message.Terminal;
                command.Parameters.Add("OsUser", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.OsUser) ? DBNull.Value : message.OsUser;
                command.Parameters.Add("Action", OracleDbType.Int32).Value = message.Action;
                command.Parameters.Add("ReturnCode", OracleDbType.Int32).Value = message.ReturnCode;
                command.Parameters.Add("Owner", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Owner) ? DBNull.Value : message.Owner;
                command.Parameters.Add("Name", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.Name) ? DBNull.Value : message.Name;
                command.Parameters.Add("AuthPrivileges", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.AuthPrivileges) ? DBNull.Value : message.AuthPrivileges;
                command.Parameters.Add("AuthGrantee", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.AuthGrantee) ? DBNull.Value : message.AuthGrantee;
                command.Parameters.Add("NewOwner", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.NewOwner) ? DBNull.Value : message.NewOwner;
                command.Parameters.Add("NewName", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(message.NewName) ? DBNull.Value : message.NewName;
                command.Parameters.Add("PrivilegeUsed", OracleDbType.Varchar2).Value = message.PrivilegeUsed ?? (object)DBNull.Value;

                // CLOB parameters for large text fields
                command.Parameters.Add("Text", OracleDbType.Clob).Value = string.IsNullOrEmpty(message.SqlText) ? DBNull.Value : message.SqlText;
                command.Parameters.Add("BindVariables", OracleDbType.Clob).Value = string.IsNullOrEmpty(message.BindVariables) ? DBNull.Value : message.BindVariables;

                command.Parameters.Add("Timestamp", OracleDbType.TimeStamp).Value = message.Timestamp;
                command.Parameters.Add("ProducedAt", OracleDbType.TimeStamp).Value = message.ProducedAt;
                command.Parameters.Add("KafkaPartition", OracleDbType.Int32).Value = partition;
                command.Parameters.Add("KafkaOffset", OracleDbType.Int64).Value = offset;

                await command.ExecuteNonQueryAsync();

                _logger.LogDebug("Inserted audit message {MessageId} (partition: {Partition}, offset: {Offset})",
                    message.Id, partition, offset);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving audit message {MessageId}", message.Id);
            throw;
        }
    }

    public async Task<bool> IsProcessedAsync(string messageId)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT COUNT(1) FROM audit_logs WHERE ID = :MessageId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { MessageId = messageId });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if message {MessageId} is processed", messageId);
            throw;
        }
    }
}
