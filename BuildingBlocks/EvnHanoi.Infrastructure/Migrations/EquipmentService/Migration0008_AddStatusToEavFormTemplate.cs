using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0008_AddStatusToEavFormTemplate : IScript
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

            // Thêm cột Status với giá trị mặc định là 'Tạo mới'
            // ORA-01430: column being added already exists in table
            ExecuteDDL("ALTER TABLE EavFormTemplates ADD Status VARCHAR2(50) DEFAULT 'Tạo mới' NULL", 1430);
            
            // Cập nhật các bản ghi cũ nếu trường Status bị NULL
            cmd.CommandText = "UPDATE EavFormTemplates SET Status = 'Tạo mới' WHERE Status IS NULL";
            cmd.ExecuteNonQuery();
        }

        return string.Empty;
    }
}
