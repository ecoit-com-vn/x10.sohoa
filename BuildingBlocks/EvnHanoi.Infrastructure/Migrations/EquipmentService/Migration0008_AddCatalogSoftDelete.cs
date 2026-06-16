using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0008_AddCatalogSoftDelete : IScript
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

            // 1. Add IsDeleted, UpdatedAt, and UpdatedBy to CATALOG_TYPE table
            ExecuteDDL("ALTER TABLE CATALOG_TYPE ADD IsDeleted NUMBER(1) DEFAULT 0 NOT NULL", 1430);
            ExecuteDDL("ALTER TABLE CATALOG_TYPE ADD UpdatedAt TIMESTAMP NULL", 1430);
            ExecuteDDL("ALTER TABLE CATALOG_TYPE ADD UpdatedBy VARCHAR2(50) NULL", 1430);

            // 2. Add IsDeleted to CATALOG table
            ExecuteDDL("ALTER TABLE CATALOG ADD IsDeleted NUMBER(1) DEFAULT 0 NOT NULL", 1430);
        }

        return string.Empty;
    }
}
