using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0027_AddCodeColumnToEavFormTemplateVersions : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.Parameters.Clear();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var ignored = false;
                foreach (var code in ignoreErrorCodes)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-0{code}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-{code}", StringComparison.OrdinalIgnoreCase))
                    {
                        ignored = true;
                        break;
                    }
                }

                if (!ignored)
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
            }
        }

        // Add Code column to EavFormTemplateVersions
        ExecuteNonQuery("ALTER TABLE EavFormTemplateVersions ADD Code VARCHAR2(100) NULL", 1430); // ORA-01430: column being added already exists in table

        // Copy Code from EavFormTemplates to EavFormTemplateVersions for existing versions
        ExecuteNonQuery(@"
            UPDATE EavFormTemplateVersions v
            SET v.Code = 
                (SELECT t.Code 
                 FROM EavFormTemplates t 
                 WHERE t.Id = v.FormTemplateId)
            WHERE v.Code IS NULL"
        );

        return string.Empty;
    }
}
