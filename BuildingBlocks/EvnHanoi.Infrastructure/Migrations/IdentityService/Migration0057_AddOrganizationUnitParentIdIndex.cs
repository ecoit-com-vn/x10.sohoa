using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Thêm IX_ORGANIZATION_UNIT_PARENTID (ParentId) — ORGANIZATION_UNIT.ParentId là self-FK dùng cho câu
/// truy vấn phân cấp "CONNECT BY PRIOR Id = ParentId" (DossierSearchService.ApplyUnitScopeAsync, chạy
/// trên MỌI lần tải danh sách hồ sơ có lọc theo đơn vị — cả màn Xuất bản hồ sơ lẫn /my-dossiers), và
/// GetOrganizationUnitsHierarchicalAsync ở nhiều nơi khác — Oracle không tự tạo index cho self-FK, nên
/// mỗi bước duyệt cây phải quét toàn bảng để tìm các con của 1 node.
///
/// Chỉ CỘNG THÊM (CREATE INDEX), không đụng dữ liệu/constraint hiện có — an toàn chạy trên môi trường
/// đang có dữ liệu. Idempotent qua bắt ORA-00955 (index đã tồn tại), cùng khuôn các migration index khác.
/// </summary>
public class Migration0057_AddOrganizationUnitParentIdIndex : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = "CREATE INDEX IX_ORGANIZATION_UNIT_PARENTID ON ORGANIZATION_UNIT (ParentId)";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
