using System;
using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

public class Migration0042_AddEncryptedKeyToExternalApiKeys : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = "ALTER TABLE EXTERNAL_API_KEYS ADD ENCRYPTED_KEY VARCHAR2(1000) NULL";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // The column already exists.
        }

        return string.Empty;
    }
}
