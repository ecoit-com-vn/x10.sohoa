using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Allows the same equipment code to exist in different infrastructures.
/// </summary>
public class Migration0038_ChangeEquipmentCodeUniqueConstraint : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        // The original constraint was unnamed, so Oracle assigned a SYS_* name that can differ by database.
        cmd.CommandText = @"
            DECLARE
            BEGIN
                FOR unique_constraint IN (
                    SELECT uc.CONSTRAINT_NAME
                    FROM USER_CONSTRAINTS uc
                    WHERE uc.TABLE_NAME = 'EQUIPMENTS'
                      AND uc.CONSTRAINT_TYPE = 'U'
                      AND EXISTS (
                          SELECT 1
                          FROM USER_CONS_COLUMNS ucc
                          WHERE ucc.CONSTRAINT_NAME = uc.CONSTRAINT_NAME
                            AND ucc.TABLE_NAME = uc.TABLE_NAME
                            AND ucc.COLUMN_NAME = 'CODE'
                      )
                      AND NOT EXISTS (
                          SELECT 1
                          FROM USER_CONS_COLUMNS ucc
                          WHERE ucc.CONSTRAINT_NAME = uc.CONSTRAINT_NAME
                            AND ucc.TABLE_NAME = uc.TABLE_NAME
                            AND ucc.COLUMN_NAME <> 'CODE'
                      )
                ) LOOP
                    EXECUTE IMMEDIATE 'ALTER TABLE EQUIPMENTS DROP CONSTRAINT ' || unique_constraint.CONSTRAINT_NAME;
                END LOOP;
            END;";
        cmd.ExecuteNonQuery();

        try
        {
            cmd.CommandText = @"
                ALTER TABLE EQUIPMENTS
                ADD CONSTRAINT UQ_EQUIPMENTS_INFRA_CODE
                UNIQUE (INFRASTRUCTURE_ID, CODE)";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-02261", StringComparison.OrdinalIgnoreCase))
        {
            // The target composite unique key already exists.
        }

        return string.Empty;
    }
}
