using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0035_AddManufactureYearAndStatusToEquipments : IScript
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

            // Năm sản xuất thiết bị
            // ORA-01430: column being added already exists in table
            ExecuteNonQuery("ALTER TABLE EQUIPMENTS ADD MANUFACTURE_YEAR NUMBER(4) NULL", 1430);

            // Tình trạng thiết bị — FK lỏng tới CATALOG.Id (catalogType = EQUIPMENT_STATUS), theo seed ở Migration0036
            ExecuteNonQuery("ALTER TABLE EQUIPMENTS ADD EQUIPMENT_STATUS_ID NUMBER NULL", 1430);
        }

        return string.Empty;
    }
}
