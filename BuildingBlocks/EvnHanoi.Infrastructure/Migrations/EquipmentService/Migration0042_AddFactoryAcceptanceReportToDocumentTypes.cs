using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Adds the factory acceptance report flag to document types.
/// </summary>
public class Migration0042_AddFactoryAcceptanceReportToDocumentTypes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();

        try
        {
            command.CommandText = @"
                ALTER TABLE DOCUMENT_TYPES
                ADD IS_FACTORY_ACCEPTANCE_REPORT NUMBER(1) DEFAULT 0 NOT NULL";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // The column already exists in this database.
        }

        return string.Empty;
    }
}
