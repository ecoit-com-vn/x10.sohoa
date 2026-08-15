using System;
using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Uses the SSO position ID directly as APP_USER.PositionId instead of keeping a duplicate column.
/// </summary>
public sealed class Migration0049_RemoveSsoPositionIdFromAppUser : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            SELECT COUNT(*) FROM USER_TAB_COLUMNS
            WHERE TABLE_NAME = 'APP_USER' AND COLUMN_NAME = 'SSO_POSITION_ID'";

        if (Convert.ToInt32(command.ExecuteScalar()) > 0)
        {
            command.CommandText = @"
                UPDATE APP_USER
                SET PositionId = TO_NUMBER(SSO_POSITION_ID)
                WHERE PositionId IS NULL
                  AND REGEXP_LIKE(SSO_POSITION_ID, '^[0-9]+$')";
            command.ExecuteNonQuery();

            command.CommandText = "ALTER TABLE APP_USER DROP COLUMN SSO_POSITION_ID";
            command.ExecuteNonQuery();
        }

        return string.Empty;
    }
}
