using System;
using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>Repairs SSO history tables created before ResourceName was added.</summary>
public sealed class Migration0056_AddSsoHistoryResourceName : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = "SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'SSO_LOGIN_HISTORY' AND COLUMN_NAME = 'RESOURCE_NAME'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            command.CommandText = "ALTER TABLE SSO_LOGIN_HISTORY ADD (RESOURCE_NAME VARCHAR2(255) DEFAULT 'Cổng SSO EVNHANOI' NULL)";
            command.ExecuteNonQuery();
        }
        return string.Empty;
    }
}