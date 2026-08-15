using System;
using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Stores the phone number received from the SSO identity profile.
/// </summary>
public sealed class Migration0047_AddPhoneNumberToAppUser : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            SELECT COUNT(*) FROM USER_TAB_COLUMNS
            WHERE TABLE_NAME = 'APP_USER' AND COLUMN_NAME = 'PHONE_NUMBER'";

        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            command.CommandText = "ALTER TABLE APP_USER ADD PHONE_NUMBER VARCHAR2(50) NULL";
            command.ExecuteNonQuery();
        }

        return string.Empty;
    }
}
