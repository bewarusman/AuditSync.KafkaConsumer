using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for managing targets in Oracle database.
/// </summary>
public class TargetRepository : ITargetRepository
{
    private readonly string _connectionString;
    private readonly ILogger<TargetRepository> _logger;

    public TargetRepository(string connectionString, ILogger<TargetRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(string targetName)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT COUNT(1) FROM targets WHERE NAME = :TargetName";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { TargetName = targetName });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if target exists: {TargetName}", targetName);
            throw;
        }
    }

    public async Task<Target?> GetByNameAsync(string targetName)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT ID, NAME, DESCRIPTION, CREATED_AT AS CreatedAt, UPDATED_AT AS UpdatedAt
                FROM targets
                WHERE NAME = :TargetName";

            var target = await connection.QuerySingleOrDefaultAsync<Target>(sql, new { TargetName = targetName });

            return target;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting target by name: {TargetName}", targetName);
            throw;
        }
    }
}
