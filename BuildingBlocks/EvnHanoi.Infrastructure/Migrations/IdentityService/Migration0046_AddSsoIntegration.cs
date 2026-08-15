using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Stores the SSO identity mapped to an existing internal account.
/// </summary>
public sealed class Migration0046_AddSsoIntegration : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        AddColumn(command, "APP_USER", "AUTH_PROVIDER", "VARCHAR2(20) DEFAULT 'LOCAL' NOT NULL");
        AddColumn(command, "APP_USER", "SSO_USER_ID", "VARCHAR2(100) NULL");
        AddColumn(command, "APP_USER", "SSO_USERNAME", "VARCHAR2(200) NULL");
        AddColumn(command, "APP_USER", "SSO_NS_ID", "VARCHAR2(100) NULL");
        AddColumn(command, "APP_USER", "SSO_DEPT_ID", "VARCHAR2(100) NULL");
        AddColumn(command, "APP_USER", "SSO_ORG_ID", "VARCHAR2(100) NULL");
        AddColumn(command, "APP_USER", "SSO_POSITION_ID", "VARCHAR2(100) NULL");
        AddColumn(command, "APP_USER", "STAFF_CODE", "VARCHAR2(100) NULL");
        CreateIndexIfMissing(
            command,
            "IX_APP_USER_SSO_USER",
            "CREATE INDEX IX_APP_USER_SSO_USER ON APP_USER(SSO_USER_ID)");
        return string.Empty;
    }

    private static void AddColumn(IDbCommand command, string table, string column, string definition)
    {
        command.CommandText = @"
            SELECT COUNT(*) FROM USER_TAB_COLUMNS
            WHERE TABLE_NAME = :TableName AND COLUMN_NAME = :ColumnName";
        AddParameter(command, "TableName", table);
        AddParameter(command, "ColumnName", column);
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        command.Parameters.Clear();
        if (exists) return;
        command.CommandText = $"ALTER TABLE {table} ADD {column} {definition}";
        command.ExecuteNonQuery();
    }

    private static void CreateIndexIfMissing(IDbCommand command, string indexName, string sql)
    {
        command.CommandText = "SELECT COUNT(*) FROM USER_INDEXES WHERE INDEX_NAME = :IndexName";
        AddParameter(command, "IndexName", indexName);
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        command.Parameters.Clear();
        if (exists) return;
        command.CommandText = sql;
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
