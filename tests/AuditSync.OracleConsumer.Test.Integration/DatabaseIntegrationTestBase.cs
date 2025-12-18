using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Integration;

/// <summary>
/// Base class for database integration tests using Testcontainers.
/// Provides Oracle container setup and teardown.
/// </summary>
public abstract class DatabaseIntegrationTestBase : IAsyncLifetime
{
    protected IContainer? OracleContainer { get; private set; }
    protected string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Note: This requires Docker Desktop running
        // For manual Oracle connection, set environment variable: INTEGRATION_TEST_ORACLE_CONN_STRING
        var manualConnectionString = Environment.GetEnvironmentVariable("INTEGRATION_TEST_ORACLE_CONN_STRING");

        if (!string.IsNullOrEmpty(manualConnectionString))
        {
            ConnectionString = manualConnectionString;
            await CreateTestSchemaAsync();
            return;
        }

        // Use Testcontainers Oracle
        OracleContainer = new ContainerBuilder()
            .WithImage("gvenzl/oracle-xe:latest")
            .WithPortBinding(1521, true)
            .WithEnvironment("ORACLE_PASSWORD", "TestPassword123")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1521))
            .Build();

        await OracleContainer.StartAsync();

        var port = OracleContainer.GetMappedPublicPort(1521);
        ConnectionString = $"User Id=system;Password=TestPassword123;Data Source=localhost:{port}/XEPDB1";

        await CreateTestSchemaAsync();
    }

    private async Task CreateTestSchemaAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        var createTablesScript = @"
            BEGIN
                EXECUTE IMMEDIATE 'DROP TABLE audit_log_extracted_values CASCADE CONSTRAINTS';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            /
            BEGIN
                EXECUTE IMMEDIATE 'DROP TABLE audit_logs CASCADE CONSTRAINTS';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            /
            BEGIN
                EXECUTE IMMEDIATE 'DROP TABLE target_rules CASCADE CONSTRAINTS';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            /
            BEGIN
                EXECUTE IMMEDIATE 'DROP TABLE targets CASCADE CONSTRAINTS';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            /

            CREATE TABLE audit_logs (
                ID VARCHAR2(100) PRIMARY KEY,
                TARGET VARCHAR2(256),
                SESSION_ID NUMBER,
                ENTRY_ID NUMBER,
                STATEMENT NUMBER,
                DB_USER VARCHAR2(128),
                USER_HOST VARCHAR2(256),
                TERMINAL VARCHAR2(128),
                OS_USER VARCHAR2(128),
                ACTION NUMBER,
                RETURN_CODE NUMBER,
                OWNER VARCHAR2(128),
                NAME VARCHAR2(128),
                AUTH_PRIVILEGES VARCHAR2(256),
                AUTH_GRANTEE VARCHAR2(128),
                NEW_OWNER VARCHAR2(128),
                NEW_NAME VARCHAR2(128),
                PRIVILEGE_USED VARCHAR2(256),
                TEXT CLOB,
                BIND_VARIABLES CLOB,
                TIMESTAMP TIMESTAMP,
                PRODUCED_AT TIMESTAMP,
                KAFKA_PARTITION NUMBER,
                KAFKA_OFFSET NUMBER,
                PROCESS_COUNTER NUMBER DEFAULT 1,
                PROCESSED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
                CONSUMED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
                CONSTRAINT UK_AUDIT_OFFSET UNIQUE (KAFKA_PARTITION, KAFKA_OFFSET)
            )
            /

            CREATE TABLE audit_log_extracted_values (
                ID VARCHAR2(100) PRIMARY KEY,
                AUDIT_MESSAGE_ID VARCHAR2(100) NOT NULL,
                FIELD_NAME VARCHAR2(100) NOT NULL,
                FIELD_VALUE VARCHAR2(4000),
                EXTRACTED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
                CONSTRAINT FK_AUDIT_MESSAGE FOREIGN KEY (AUDIT_MESSAGE_ID)
                    REFERENCES audit_logs(ID) ON DELETE CASCADE
            )
            /

            CREATE TABLE targets (
                ID VARCHAR2(100) PRIMARY KEY,
                NAME VARCHAR2(256) NOT NULL UNIQUE,
                DESCRIPTION VARCHAR2(1000),
                CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
                UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
            )
            /

            CREATE TABLE target_rules (
                ID VARCHAR2(100) PRIMARY KEY,
                TARGET_ID VARCHAR2(100) NOT NULL,
                RULE_NAME VARCHAR2(100) NOT NULL,
                SOURCE_FIELD VARCHAR2(100) NOT NULL,
                REGEX_PATTERN VARCHAR2(1000) NOT NULL,
                IS_REQUIRED NUMBER(1) DEFAULT 0,
                IS_ACTIVE NUMBER(1) DEFAULT 1,
                RULE_ORDER NUMBER NOT NULL,
                CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
                UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
                CONSTRAINT FK_TARGET FOREIGN KEY (TARGET_ID)
                    REFERENCES targets(ID) ON DELETE CASCADE,
                CONSTRAINT UK_TARGET_RULE UNIQUE (TARGET_ID, RULE_NAME)
            )
            /
        ";

        using var command = connection.CreateCommand();
        command.CommandText = createTablesScript;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (OracleContainer != null)
        {
            await OracleContainer.DisposeAsync();
        }
    }

    protected ILogger<T> CreateLogger<T>()
    {
        return NullLogger<T>.Instance;
    }
}
