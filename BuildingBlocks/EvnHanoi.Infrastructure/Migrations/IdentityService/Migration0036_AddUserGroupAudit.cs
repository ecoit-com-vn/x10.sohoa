using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Bổ sung thời điểm và người tạo cho nhóm người dùng.
/// </summary>
public sealed class Migration0036_AddUserGroupAudit : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        ExecuteIgnore(command, "ALTER TABLE USER_GROUP ADD CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP", "ORA-01430");
        ExecuteIgnore(command, "ALTER TABLE USER_GROUP ADD CreatedBy VARCHAR2(50)", "ORA-01430");
        ExecuteIgnore(command, "UPDATE USER_GROUP SET CreatedAt = CURRENT_TIMESTAMP WHERE CreatedAt IS NULL", "ORA-00904");
        return string.Empty;
    }

    private static void ExecuteIgnore(IDbCommand command, string sql, string ignoredError)
    {
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception exception) when (exception.Message.Contains(ignoredError, StringComparison.OrdinalIgnoreCase))
        {
            // Idempotent when the column already exists.
        }
    }
}
