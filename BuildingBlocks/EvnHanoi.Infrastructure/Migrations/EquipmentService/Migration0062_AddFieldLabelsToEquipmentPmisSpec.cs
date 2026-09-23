using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Bổ sung cột lưu nhãn tiếng Việt của từng khoá thông số kỹ thuật PMIS (field "tenThongSoKyThuat",
/// PMIS bổ sung 2026-09-23 vào cả 3 API DanhSachThietBi/DanhSachThietBiDuongDay/ChiTietThietBi — xem
/// BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md) — song song với FORM_VALUES (giá trị, field "thongSoKyThuat")
/// đã có sẵn từ Migration0049. Dùng để gợi ý nhãn thật cho admin khi khai "Tên trường PMIS" trong Form
/// Builder (EquipmentController.GetPmisSpecKeys), thay vì phải đoán ý nghĩa khoá UPPER_SNAKE
/// (DUNG_LUONG, I_DM...) chỉ từ giá trị mẫu như trước đây.
/// </summary>
public class Migration0062_AddFieldLabelsToEquipmentPmisSpec : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = "ALTER TABLE EQUIPMENT_PMIS_SPEC ADD (FieldLabels CLOB NULL)";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại.
        }

        return string.Empty;
    }
}
