using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Adds an optional note for equipment records.
/// </summary>
public class Migration0040_AddNoteToEquipments : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = @"
                ALTER TABLE EQUIPMENTS
                ADD Note CLOB NULL";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // The column already exists in this database.
        }

        return string.Empty;
    }
}
