using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0057_AddParentIdToInfrastructure : IScript
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

            // Quan hệ cha-con giữa các Trạm/Đường dây (dùng cho hiển thị cây ở màn Đường dây)
            // ORA-01430: column being added already exists in table
            ExecuteNonQuery("ALTER TABLE INFRASTRUCTURE ADD PARENT_ID VARCHAR2(36) NULL", 1430);

            // ORA-02275: such a referential constraint already exists in the table
            // ORA-00955: name is already used by an existing object
            ExecuteNonQuery("ALTER TABLE INFRASTRUCTURE ADD CONSTRAINT fk_infra_parent FOREIGN KEY (PARENT_ID) REFERENCES INFRASTRUCTURE(ID) ON DELETE SET NULL", 2275, 955);
        }

        return string.Empty;
    }
}
