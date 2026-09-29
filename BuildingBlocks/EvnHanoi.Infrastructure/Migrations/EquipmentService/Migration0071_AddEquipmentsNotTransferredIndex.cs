using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm index hàm IX_EQUIPMENTS_NOT_TRANSFERRED — khớp CHÍNH XÁC biểu thức
/// EquipmentSqlFilters.NotTransferredAway() dùng để loại thiết bị "hồn ma" (StatusTransition=0, "Đã
/// chuyển TBA") khỏi mọi truy vấn liệt kê/đếm thiết bị đang hoạt động (áp dụng ở hầu hết mọi câu tra
/// cứu EQUIPMENTS không giới hạn theo 1 Trạm/Đường dây cụ thể — đây là điều kiện lọc gần như luôn có
/// mặt).
///
/// PHÁT HIỆN QUA EXPLAIN PLAN THẬT (audit hiệu năng /equipment/device-list 2026-09-29): điều kiện
/// "(StatusTransition IS NULL OR StatusTransition &lt;&gt; 0)" thực ra RẤT chọn lọc — chỉ ~7.015/160.781
/// dòng khớp (~4.4%), tức loại bỏ ~95.6% số dòng — NHƯNG Oracle vẫn full table scan vì B-tree index
/// thường không lưu giá trị NULL, nên nhánh "IS NULL" luôn buộc quét toàn bảng dù có index trên
/// StatusTransition. Migration0069 (index UnitId/EquipmentTypeId/IsActive/(IsDeleted,CreatedAt)) không
/// giải quyết được vấn đề này vì không đụng tới StatusTransition.
///
/// GIẢI PHÁP: index hàm (function-based index) trên biểu thức
/// "CASE WHEN (StatusTransition IS NULL OR StatusTransition &lt;&gt; 0) THEN 1 END" — dòng "hồn ma"
/// (95.6% còn lại) có CASE trả về NULL nên KHÔNG được lưu vào index luôn (đúng kiểu index hàm CASE WHEN
/// đã dùng ở UX_EQUIPMENTS_ACTIVE_INFRA_CODE, Migration0059_FixEquipmentsInfraCodeUnique) — index chỉ
/// còn ~4.4% dòng "sống", cực nhỏ so với toàn bảng. EquipmentSqlFilters.NotTransferredAway() đã được sửa
/// để sinh ra ĐÚNG biểu thức "(CASE WHEN (...) THEN 1 END) = 1" này (Oracle chỉ dùng được function-based
/// index khi WHERE khớp CHÍNH XÁC biểu thức lúc tạo index).
///
/// Chỉ CỘNG THÊM (CREATE INDEX), không đụng dữ liệu/constraint hiện có — an toàn chạy trên môi trường
/// đang có dữ liệu. Idempotent qua bắt ORA-00955 (index đã tồn tại).
/// </summary>
public class Migration0071_AddEquipmentsNotTransferredIndex : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = @"
                CREATE INDEX IX_EQUIPMENTS_NOT_TRANSFERRED ON EQUIPMENTS (
                    CASE WHEN (StatusTransition IS NULL OR StatusTransition <> 0) THEN 1 END
                )";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
