using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0003_AddCatalogTypeFields : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            void ExecuteDDL(string sql, params int[] ignoreErrorCodes)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
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

            // 1. Add IsPrivate and Status columns to CATALOG_TYPE table
            // Ignore ORA-01430: column being added already exists
            ExecuteDDL("ALTER TABLE CATALOG_TYPE ADD IsPrivate NUMBER(1) DEFAULT 0 NOT NULL", 1430);
            ExecuteDDL("ALTER TABLE CATALOG_TYPE ADD Status NUMBER(1) DEFAULT 1 NOT NULL", 1430);
        }

        return string.Empty;
    }
}
