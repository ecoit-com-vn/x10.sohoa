using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Bổ sung cấp lưới điện "Hạ áp" (Id = 3) vào GridTypes — trước đây bảng chỉ có 2 dòng (1 Cao áp,
/// 2 Trung áp, seed từ 0006_ModifyEquipmentTypesSchema.sql) nên thiết bị PMIS cấp dưới 1kV (dữ liệu
/// thật trả về "0,4kV", "0,22kV") bị dồn nhầm vào Trung áp. Xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md §7.3.
///
/// Chỉ thêm danh mục — loại thiết bị hạ áp tương ứng trong EquipmentTypes do Admin tự tạo, giống các
/// cấp còn lại (không tự sinh, tránh đoán sai danh mục thiết bị).
/// </summary>
public class Migration0053_AddLowVoltageGridType : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = "INSERT INTO GridTypes (Id, Name) VALUES (3, 'Hạ áp')";

        try
        {
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
        {
            // Đã có dòng Id = 3 (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
