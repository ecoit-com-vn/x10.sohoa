using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0003_RenameEavSchemaColumn : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            // Helper method to execute DDL and ignore specific Oracle error codes
            void ExecuteDDL(string sql, params int[] ignoreErrorCodes)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    // Check if exception contains any of the Oracle error codes to ignore
                    bool ignored = false;
                    foreach (var code in ignoreErrorCodes)
                    {
                        if (ex.Message.Contains($"ORA-{code:D5}") || ex.Message.Contains($"ORA-0{code}") || ex.Message.Contains($"ORA-{code}"))
                        {
                            ignored = true;
                            break;
                        }
                    }
                    if (!ignored)
                    {
                        throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
                    }
                }
            }

            // Rename Schema to FormSchema in EavFormTemplates table
            // Ignore ORA-00904 (column doesn't exist to rename - already renamed)
            ExecuteDDL("ALTER TABLE EavFormTemplates RENAME COLUMN \"Schema\" TO FormSchema", 904);
        }

        return string.Empty;
    }
}
