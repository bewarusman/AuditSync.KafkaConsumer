using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace AuditSync.OracleConsumer.Infrastructure.Repositories;

/// <summary>
/// Repository for managing cases in Oracle database.
/// </summary>
public class CaseRepository : ICaseRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CaseRepository> _logger;

    public CaseRepository(string connectionString, ILogger<CaseRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<string> CreateAsync(Case @case)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO cases (ID, AUDIT_LOG_ID, CASE_STATUS, VALID, CREATED_AT, UPDATED_AT,
                                   RESOLVED_AT, RESOLVED_BY, RESOLUTION_NOTES)
                VALUES (:Id, :AuditLogId, :CaseStatus, :Valid, :CreatedAt, :UpdatedAt,
                        :ResolvedAt, :ResolvedBy, :ResolutionNotes)";

            await connection.ExecuteAsync(sql, new
            {
                Id = @case.Id,
                AuditLogId = @case.AuditLogId,
                CaseStatus = @case.CaseStatus,
                Valid = @case.Valid,
                CreatedAt = @case.CreatedAt,
                UpdatedAt = @case.UpdatedAt,
                ResolvedAt = @case.ResolvedAt,
                ResolvedBy = @case.ResolvedBy,
                ResolutionNotes = @case.ResolutionNotes
            });

            _logger.LogDebug("Created case {CaseId} for audit log {AuditLogId}", @case.Id, @case.AuditLogId);

            return @case.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating case for audit log {AuditLogId}", @case.AuditLogId);
            throw;
        }
    }

    public async Task<bool> ExistsForAuditLogAsync(string auditLogId)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT COUNT(1) FROM cases WHERE AUDIT_LOG_ID = :AuditLogId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { AuditLogId = auditLogId });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if case exists for audit log {AuditLogId}", auditLogId);
            throw;
        }
    }

    public async Task<Case?> GetByIdAsync(string caseId)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT ID, AUDIT_LOG_ID as AuditLogId, CASE_STATUS as CaseStatus, VALID,
                       CREATED_AT as CreatedAt, UPDATED_AT as UpdatedAt, RESOLVED_AT as ResolvedAt,
                       RESOLVED_BY as ResolvedBy, RESOLUTION_NOTES as ResolutionNotes
                FROM cases
                WHERE ID = :CaseId";

            var result = await connection.QueryFirstOrDefaultAsync<Case>(sql, new { CaseId = caseId });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting case by ID {CaseId}", caseId);
            throw;
        }
    }

    public async Task<Case?> GetByAuditLogIdAsync(string auditLogId)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT ID, AUDIT_LOG_ID as AuditLogId, CASE_STATUS as CaseStatus, VALID,
                       CREATED_AT as CreatedAt, UPDATED_AT as UpdatedAt, RESOLVED_AT as ResolvedAt,
                       RESOLVED_BY as ResolvedBy, RESOLUTION_NOTES as ResolutionNotes
                FROM cases
                WHERE AUDIT_LOG_ID = :AuditLogId";

            var result = await connection.QueryFirstOrDefaultAsync<Case>(sql, new { AuditLogId = auditLogId });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting case by audit log ID {AuditLogId}", auditLogId);
            throw;
        }
    }
}
