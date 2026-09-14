using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// EQUIPMENTS.INFRASTRUCTURE_ID có FK (fk_equip_infra) nhưng Oracle không tự tạo index cho FK — cột này
/// bị scan tuần tự mỗi khi lọc thiết bị theo 1 Trạm/Đường dây (vd. PmisDocumentRepository.GetCatalogTreeAsync
/// đếm tài liệu thiết bị con cho từng INFRASTRUCTURE), gây timeout khi 2 bảng đủ lớn.
/// </summary>
public class Migration0059_AddInfrastructureIdIndexToEquipments : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();
        try
        {
            cmd.CommandText = "CREATE INDEX IDX_EQUIPMENTS_INFRASTRUCTURE_ID ON EQUIPMENTS (INFRASTRUCTURE_ID)";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại.
        }

        return string.Empty;
    }
}
