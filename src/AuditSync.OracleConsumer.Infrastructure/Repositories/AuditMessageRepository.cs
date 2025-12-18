using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

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

            // MERGE statement: INSERT new record or UPDATE existing (incrementing PROCESS_COUNTER)
            var sql = @"
                MERGE INTO audit_logs dest
                USING (SELECT :Id AS ID FROM DUAL) src
                ON (dest.ID = src.ID)
                WHEN MATCHED THEN
                    UPDATE SET
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
                WHEN NOT MATCHED THEN
                    INSERT (ID, TARGET, SESSION_ID, ENTRY_ID, STATEMENT, DB_USER, USER_HOST, TERMINAL, OS_USER,
                            ACTION, RETURN_CODE, OWNER, NAME, AUTH_PRIVILEGES, AUTH_GRANTEE, NEW_OWNER, NEW_NAME,
                            PRIVILEGE_USED, TEXT, BIND_VARIABLES, TIMESTAMP, PRODUCED_AT, KAFKA_PARTITION, KAFKA_OFFSET,
                            PROCESS_COUNTER, PROCESSED_AT, CONSUMED_AT)
                    VALUES (:Id, :Target, :SessionId, :EntryId, :Statement, :DbUser, :UserHost, :Terminal, :OsUser,
                            :Action, :ReturnCode, :Owner, :Name, :AuthPrivileges, :AuthGrantee, :NewOwner, :NewName,
                            :PrivilegeUsed, :Text, :BindVariables, :Timestamp, :ProducedAt, :KafkaPartition, :KafkaOffset,
                            1, SYSTIMESTAMP, SYSTIMESTAMP)";

            var parameters = new
            {
                Id = message.Id,
                Target = message.Target,
                SessionId = message.SessionId,
                EntryId = message.EntryId,
                Statement = message.Statement,
                DbUser = message.DbUser,
                UserHost = message.UserHost,
                Terminal = message.Terminal,
                OsUser = message.OsUser,
                Action = message.Action,
                ReturnCode = message.ReturnCode,
                Owner = message.Owner,
                Name = message.Name,
                AuthPrivileges = message.AuthPrivileges,
                AuthGrantee = message.AuthGrantee,
                NewOwner = message.NewOwner,
                NewName = message.NewName,
                PrivilegeUsed = message.PrivilegeUsed,
                Text = message.SqlText,
                BindVariables = message.BindVariables,
                Timestamp = message.Timestamp,
                ProducedAt = message.ProducedAt,
                KafkaPartition = partition,
                KafkaOffset = offset
            };

            await connection.ExecuteAsync(sql, parameters);

            _logger.LogDebug("Saved audit message {MessageId} (partition: {Partition}, offset: {Offset})",
                message.Id, partition, offset);
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
