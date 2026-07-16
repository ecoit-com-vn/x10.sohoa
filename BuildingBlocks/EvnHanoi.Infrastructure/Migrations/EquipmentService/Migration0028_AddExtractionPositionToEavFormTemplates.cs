using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0028_AddExtractionPositionToEavFormTemplates : IScript
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

        // Add ExtractionPosition column to EavFormTemplates
        ExecuteNonQuery("ALTER TABLE EavFormTemplates ADD ExtractionPosition VARCHAR2(100) NULL", 1430); // ORA-01430: column being added already exists in table

        // Add ExtractionPosition column to EavFormTemplateVersions
        ExecuteNonQuery("ALTER TABLE EavFormTemplateVersions ADD ExtractionPosition VARCHAR2(100) NULL", 1430); // ORA-01430: column being added already exists in table

        // Update default values to 'all' for existing templates and versions
        ExecuteNonQuery("UPDATE EavFormTemplates SET ExtractionPosition = 'all' WHERE ExtractionPosition IS NULL");
        ExecuteNonQuery("UPDATE EavFormTemplateVersions SET ExtractionPosition = 'all' WHERE ExtractionPosition IS NULL");

        return string.Empty;
    }
}
