using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Adds the optional equipment transfer status used when an equipment is moved between infrastructures.
/// </summary>
public class Migration0039_AddStatusTransitionToEquipments : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = @"
                ALTER TABLE EQUIPMENTS
                ADD StatusTransition NUMBER(1) NULL";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // The column already exists in this database.
        }

        return string.Empty;
    }
}
