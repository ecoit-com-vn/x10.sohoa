using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0037_AddOperationDateToInfrastructure : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
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

            // Ngày vận hành của Trạm/Đường dây (INFRA_TYPE_ID = 1/2)
            // ORA-01430: column being added already exists in table
            ExecuteNonQuery("ALTER TABLE INFRASTRUCTURE ADD OPERATION_DATE TIMESTAMP NULL", 1430);
        }

        return string.Empty;
    }
}
