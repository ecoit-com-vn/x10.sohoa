using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// PMIS vừa bổ sung field "maCMIS" vào response của DanhSachTBA (SUBSTATION_LIST) và DanhSachDuongDay
/// (LINE_LIST) (phát hiện 2026-09-24, xem pmis-api-responses/README.md) — mã của hệ thống CMIS (Customer
/// Management/Care Information System, hệ quản lý khách hàng/lưới điện cũ của EVN, khác PMIS), định dạng
/// khác hẳn PMIS_CODE (VD "PD0284251", không dấu "."/"-"). Chỉ lưu tham khảo/hiển thị — KHÔNG dùng làm
/// khoá tra cứu/đối chiếu, giống PMIS_CODE/LAST_SYNCED_FROM_PMIS_AT lúc mới thêm (xem Migration0047).
/// Không thêm index — chưa có nhu cầu lọc/tìm theo CMIS_CODE.
/// </summary>
public class Migration0068_AddCmisCodeToInfrastructure : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = "ALTER TABLE INFRASTRUCTURE ADD CMIS_CODE VARCHAR2(100) NULL";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
