using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Khóa map Trạm/Đường dây (INFRASTRUCTURE, phân biệt bằng INFRA_TYPE_ID) với mã PMIS
/// (maTBA/maDuongDay) để đồng bộ dữ liệu từ PMIS — xem tính năng "Đồng bộ PMIS".
/// </summary>
public class Migration0047_AddPmisFieldsToInfrastructure : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = "ALTER TABLE INFRASTRUCTURE ADD (PMIS_CODE VARCHAR2(100) NULL, LAST_SYNCED_FROM_PMIS_AT TIMESTAMP NULL)";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại.
        }

        try
        {
            cmd.CommandText = "CREATE INDEX IDX_INFRASTRUCTURE_PMIS_CODE ON INFRASTRUCTURE (PMIS_CODE)";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại.
        }

        return string.Empty;
    }
}
