namespace AuditSync.OracleConsumer.Domain.Models;

/// <summary>
/// Extension methods and helpers for SourceFieldType enum.
/// </summary>
public static class SourceFieldTypeExtensions
{
    /// <summary>
    /// Converts a string field name to the corresponding SourceFieldType enum value.
    /// Used primarily in tests to maintain compatibility with string-based field names.
    /// </summary>
    public static int ToSourceFieldType(this string fieldName)
    {
        return fieldName.ToLower() switch
        {
            "text" or "sqltext" => (int)SourceFieldType.SqlText,
            "bindvariables" => (int)SourceFieldType.BindVariables,
            "owner" => (int)SourceFieldType.Owner,
            "name" => (int)SourceFieldType.Name,
            "dbuser" => (int)SourceFieldType.DbUser,
            "userhost" => (int)SourceFieldType.UserHost,
            "terminal" => (int)SourceFieldType.Terminal,
            "osuser" => (int)SourceFieldType.OsUser,
            "target" => (int)SourceFieldType.Target,
            "authprivileges" => (int)SourceFieldType.AuthPrivileges,
            "authgrantee" => (int)SourceFieldType.AuthGrantee,
            "newowner" => (int)SourceFieldType.NewOwner,
            "newname" => (int)SourceFieldType.NewName,
            "privilegeused" => (int)SourceFieldType.PrivilegeUsed,
            _ => throw new ArgumentException($"Unknown source field name: {fieldName}", nameof(fieldName))
        };
    }

    /// <summary>
    /// Converts a SourceFieldType enum value (or int) to a human-readable string.
    /// </summary>
    public static string ToFieldName(this int sourceFieldType)
    {
        return (SourceFieldType)sourceFieldType switch
        {
            SourceFieldType.SqlText => "SqlText",
            SourceFieldType.BindVariables => "BindVariables",
            SourceFieldType.Owner => "Owner",
            SourceFieldType.Name => "Name",
            SourceFieldType.DbUser => "DbUser",
            SourceFieldType.UserHost => "UserHost",
            SourceFieldType.Terminal => "Terminal",
            SourceFieldType.OsUser => "OsUser",
            SourceFieldType.Target => "Target",
            SourceFieldType.AuthPrivileges => "AuthPrivileges",
            SourceFieldType.AuthGrantee => "AuthGrantee",
            SourceFieldType.NewOwner => "NewOwner",
            SourceFieldType.NewName => "NewName",
            SourceFieldType.PrivilegeUsed => "PrivilegeUsed",
            _ => "Unknown"
        };
    }
}
