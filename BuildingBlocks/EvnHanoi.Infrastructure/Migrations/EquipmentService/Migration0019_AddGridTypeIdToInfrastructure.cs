using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0019_AddGridTypeIdToInfrastructure : IScript
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

            // 1. Thêm cột GridTypeId vào bảng INFRASTRUCTURE
            // ORA-01430: column being added already exists in table
            ExecuteNonQuery("ALTER TABLE INFRASTRUCTURE ADD GridTypeId INT NULL", 1430);

            // 2. Cập nhật dữ liệu mặc định cho các trạm/đường dây hiện có (mặc định là Cao áp - 1)
            ExecuteNonQuery("UPDATE INFRASTRUCTURE SET GridTypeId = 1 WHERE GridTypeId IS NULL");

            // 3. Thêm ràng buộc khóa ngoại tới bảng GridTypes
            // ORA-02275: such a referential constraint already exists in the table
            // ORA-02264: name already used by an existing constraint
            ExecuteNonQuery("ALTER TABLE INFRASTRUCTURE ADD CONSTRAINT fk_infra_gridtype FOREIGN KEY (GridTypeId) REFERENCES GridTypes(Id)", 2275, 2264);
        }

        return string.Empty;
    }
}
