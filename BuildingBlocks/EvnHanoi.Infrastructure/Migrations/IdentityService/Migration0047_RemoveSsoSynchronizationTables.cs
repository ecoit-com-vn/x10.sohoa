using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Removes the former SSO authorization and catalog synchronization storage.
/// </summary>
public sealed class Migration0047_RemoveSsoSynchronizationTables : IScript
{
    private static readonly string[] Tables =
    [
        "SSO_USER_PERMISSION",
        "SSO_USER_ROLE",
        "SSO_POSITION_CATALOG",
        "SSO_USER_GROUP_CATALOG"
    ];

    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        foreach (var table in Tables)
        {
            DropTableIfExists(command, table);
        }

        DropIndexIfExists(command, "IX_ORG_UNIT_SSO_EXT");
        DropColumnIfExists(command, "ORGANIZATION_UNIT", "SSO_EXTERNAL_ID");
        DropColumnIfExists(command, "ORGANIZATION_UNIT", "SSO_ENTITY_TYPE");
        return string.Empty;
    }

    private static void DropTableIfExists(IDbCommand command, string table)
    {
        command.CommandText = "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = :ObjectName";
        AddParameter(command, "ObjectName", table);
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        command.Parameters.Clear();
        if (!exists) return;
        command.CommandText = $"DROP TABLE {table} CASCADE CONSTRAINTS PURGE";
        command.ExecuteNonQuery();
    }

    private static void DropIndexIfExists(IDbCommand command, string indexName)
    {
        command.CommandText = "SELECT COUNT(*) FROM USER_INDEXES WHERE INDEX_NAME = :ObjectName";
        AddParameter(command, "ObjectName", indexName);
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        command.Parameters.Clear();
        if (!exists) return;
        command.CommandText = $"DROP INDEX {indexName}";
        command.ExecuteNonQuery();
    }

    private static void DropColumnIfExists(IDbCommand command, string table, string column)
    {
        command.CommandText = @"
            SELECT COUNT(*) FROM USER_TAB_COLUMNS
            WHERE TABLE_NAME = :TableName AND COLUMN_NAME = :ColumnName";
        AddParameter(command, "TableName", table);
        AddParameter(command, "ColumnName", column);
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        command.Parameters.Clear();
        if (!exists) return;
        command.CommandText = $"ALTER TABLE {table} DROP COLUMN {column}";
        command.ExecuteNonQuery();
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
