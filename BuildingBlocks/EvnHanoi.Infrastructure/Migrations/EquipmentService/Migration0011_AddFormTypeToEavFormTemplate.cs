using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0011_AddFormTypeToEavFormTemplate : IScript
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

            // 1. Thêm cột FormType vào bảng EavFormTemplates (mặc định là 'FORM')
            // ORA-01430: column being added already exists in table
            ExecuteNonQuery("ALTER TABLE EavFormTemplates ADD FormType VARCHAR2(50) DEFAULT 'FORM' NULL", 1430);

            // 2. Cập nhật các dòng dữ liệu hiện tại
            // Các dòng có EquipmentTypeId khác NULL thì cập nhật thành 'TEMPLATE'
            ExecuteNonQuery("UPDATE EavFormTemplates SET FormType = 'TEMPLATE' WHERE EquipmentTypeId IS NOT NULL");

            // Các dòng có EquipmentTypeId bằng NULL thì cập nhật thành 'FORM'
            ExecuteNonQuery("UPDATE EavFormTemplates SET FormType = 'FORM' WHERE EquipmentTypeId IS NULL");
        }

        return string.Empty;
    }
}
