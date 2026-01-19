namespace AuditSync.OracleConsumer.Domain.Models;

/// <summary>
/// Enum representing the source field types for extraction rules.
/// Maps to the SOURCE_FIELD column in the target_rules table.
/// </summary>
public enum SourceFieldType
{
    SqlText = 0,
    BindVariables = 1,
    Owner = 2,
    Name = 3,
    DbUser = 4,
    UserHost = 5,
    Terminal = 6,
    OsUser = 7,
    Target = 8,
    AuthPrivileges = 9,
    AuthGrantee = 10,
    NewOwner = 11,
    NewName = 12,
    PrivilegeUsed = 13
}
