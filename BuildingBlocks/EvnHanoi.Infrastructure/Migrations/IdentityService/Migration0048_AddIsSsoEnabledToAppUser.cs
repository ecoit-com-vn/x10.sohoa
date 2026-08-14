using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Adds the administrative opt-in flag for SSO authentication.
/// </summary>
public sealed class Migration0048_AddIsSsoEnabledToAppUser : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM USER_TAB_COLUMNS
            WHERE TABLE_NAME = 'APP_USER'
              AND COLUMN_NAME = 'IS_SSO_ENABLED'";

        if (Convert.ToInt32(command.ExecuteScalar()) > 0)
        {
            return string.Empty;
        }

        command.CommandText = @"
            ALTER TABLE APP_USER
            ADD IS_SSO_ENABLED NUMBER(1) DEFAULT 0 NOT NULL";
        command.ExecuteNonQuery();
        return string.Empty;
    }
}
