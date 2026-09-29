using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm các index còn thiếu phục vụ EquipmentRepository.GetPagedAsync (màn "Quản lý thiết bị",
/// /equipment/device-list) — trước đây không index nào khớp UnitId/EquipmentTypeId/IsActive, và không
/// index nào khớp thứ tự ORDER BY mặc định, nên mỗi lần tải danh sách (kể cả không lọc gì) đều full
/// table scan + sort toàn bộ EQUIPMENTS, chi phí tăng thẳng theo số dòng của bảng:
///
/// - IX_EQUIPMENTS_UNITID (UnitId): khớp filter theo đơn vị (unitId cụ thể HOẶC UnitId IN
///   authorizedUnitIds — luôn áp dụng khi người dùng không chọn unitId cụ thể).
/// - IX_EQUIPMENTS_EQUIPMENTTYPEID (EquipmentTypeId): khớp filter theo loại thiết bị.
/// - IX_EQUIPMENTS_ISACTIVE (IS_ACTIVE): khớp filter theo trạng thái hoạt động.
/// - IX_EQUIPMENTS_DELETED_CREATED (IsDeleted, CreatedAt): IsDeleted=0 có mặt ở MỌI câu truy vấn danh
///   sách; ghép thêm CreatedAt cho phép Oracle đọc index theo đúng thứ tự ORDER BY e.CreatedAt DESC
///   (đọc ngược index) thay vì phải sort riêng toàn bộ tập kết quả — quan trọng nhất với lần tải trang
///   1 KHÔNG có bộ lọc nào khác (trường hợp phổ biến nhất, trước đây luôn full scan + sort).
///
/// Tất cả đều CHỈ CỘNG THÊM (CREATE INDEX), không đụng dữ liệu/constraint hiện có — an toàn chạy trên môi
/// trường đang có dữ liệu. Idempotent qua bắt ORA-00955 (index đã tồn tại), cùng khuôn Migration0065.
/// </summary>
public class Migration0069_AddEquipmentListIndexes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory,
            "CREATE INDEX IX_EQUIPMENTS_UNITID ON EQUIPMENTS (UnitId)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_EQUIPMENTS_EQUIPMENTTYPEID ON EQUIPMENTS (EquipmentTypeId)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_EQUIPMENTS_ISACTIVE ON EQUIPMENTS (IS_ACTIVE)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_EQUIPMENTS_DELETED_CREATED ON EQUIPMENTS (IsDeleted, CreatedAt)");

        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }
    }
}
