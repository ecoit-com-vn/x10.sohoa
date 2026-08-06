using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Adds a confirmation flag (0/1) for equipment records.
/// </summary>
public class Migration0046_AddIsConfirmToEquipments : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = @"
                ALTER TABLE EQUIPMENTS
                ADD IS_CONFIRM NUMBER(1) DEFAULT 0 NOT NULL";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // The column already exists in this database.
        }

        try
        {
            cmd.CommandText = @"
                ALTER TABLE EQUIPMENTS
                ADD CONSTRAINT CK_EQUIPMENTS_IS_CONFIRM CHECK (IS_CONFIRM IN (0, 1))";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-02264", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("ORA-02261", StringComparison.OrdinalIgnoreCase))
        {
            // The constraint already exists in this database.
        }

        return string.Empty;
    }
}
