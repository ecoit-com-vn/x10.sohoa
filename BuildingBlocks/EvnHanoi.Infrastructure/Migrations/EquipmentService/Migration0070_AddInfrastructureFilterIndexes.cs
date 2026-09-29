using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm các index còn thiếu phục vụ InfrastructureRepository.GetPagedAsync (màn "Quản lý trạm biến áp"
/// /catalog/substation và "Quản lý đường dây" /catalog/transmission-line) — Migration0065 đã thêm index
/// cho (INFRA_TYPE_ID, IsDeleted) và PARENT_ID, nhưng UNIT_ID/IS_ACTIVE/GRIDTYPEID (lọc theo đơn vị,
/// trạng thái, cấp điện áp — đều là filter phổ biến trên 2 màn này) vẫn chưa có index, buộc Oracle phải
/// quét toàn bộ tập con đã lọc theo INFRA_TYPE_ID rồi lọc tiếp thủ công mỗi lần áp thêm điều kiện này:
///
/// - IX_INFRASTRUCTURE_UNITID (UNIT_ID): khớp filter theo đơn vị (unitId cụ thể HOẶC UNIT_ID IN
///   danh sách đơn vị được phép xem — luôn áp dụng khi người dùng không chọn 1 đơn vị cụ thể).
/// - IX_INFRASTRUCTURE_ISACTIVE (IS_ACTIVE): khớp filter theo trạng thái hoạt động.
/// - IX_INFRASTRUCTURE_GRIDTYPEID (GRIDTYPEID): khớp filter theo cấp điện áp.
///
/// Tất cả đều CHỈ CỘNG THÊM (CREATE INDEX), không đụng dữ liệu/constraint hiện có — an toàn chạy trên môi
/// trường đang có dữ liệu. Idempotent qua bắt ORA-00955 (index đã tồn tại), cùng khuôn Migration0065/0069.
/// </summary>
public class Migration0070_AddInfrastructureFilterIndexes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory,
            "CREATE INDEX IX_INFRASTRUCTURE_UNITID ON INFRASTRUCTURE (UNIT_ID)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_INFRASTRUCTURE_ISACTIVE ON INFRASTRUCTURE (IS_ACTIVE)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_INFRASTRUCTURE_GRIDTYPEID ON INFRASTRUCTURE (GRIDTYPEID)");

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
