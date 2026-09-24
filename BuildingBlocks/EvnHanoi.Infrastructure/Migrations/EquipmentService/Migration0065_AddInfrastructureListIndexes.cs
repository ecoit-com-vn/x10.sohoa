using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm các index còn thiếu phục vụ những câu tra cứu nóng nhất của màn Danh mục Trạm/Đường dây và các
/// tra cứu liên quan, phát hiện ở đợt audit PMIS 2026-09-24 (full table scan trên ~38k dòng INFRASTRUCTURE
/// mỗi lần tải trang):
///
/// - IX_INFRASTRUCTURE_TYPE_DELETED (INFRA_TYPE_ID, IsDeleted): khớp đúng WHERE cơ bản của
///   InfrastructureRepository.GetPagedAsync (<c>WHERE i.IsDeleted = 0 AND i.INFRA_TYPE_ID = :InfraTypeId</c>) —
///   trước đây không có index nào khớp cả 2 cột lọc luôn dùng cùng nhau, Oracle buộc full scan rồi lọc.
/// - IX_INFRASTRUCTURE_PARENT_ID (PARENT_ID): phục vụ subquery ChildLineCount trong GetPagedAsync/
///   GetChildLinesAsync và chính GetChildLinesAsync (<c>WHERE i.PARENT_ID = :ParentId</c> /
///   <c>WHERE c.PARENT_ID = i.Id</c>) — PARENT_ID là self-FK, Oracle không tự tạo index cho FK.
/// - IX_INFRASTRUCTURE_CODE_NORM (UPPER(TRIM(CODE))): khớp đúng biểu thức chuẩn hoá mới của
///   InfrastructureRepository.GetByCodeAsync sau khi đổi từ LOWER(Code) sang UPPER(TRIM(Code)) (cùng đợt
///   sửa này) — index THƯỜNG (không UNIQUE), không đụng UQ_INFRASTRUCTURE_ACTIVE_CODE (Migration0058,
///   index hàm dạng CASE WHEN khác cấu trúc, không khớp được biểu thức trần UPPER(TRIM(CODE))).
/// - IX_PMIS_EQTYPE_MAPPING_LOOKUP (PmisMaLoaiTB, GridTypeId, IsDeleted) trên PMIS_EQUIPMENT_TYPE_MAPPING:
///   khớp đúng WHERE của EquipmentRepository (tra loại thiết bị theo mã PMIS + cấp điện áp) — bảng nhỏ
///   (&lt;200 dòng) nên ưu tiên thấp, nhưng thêm sẵn cho nhất quán vì index CASE WHEN hiện có
///   (Migration0054) không khớp WHERE thật.
///
/// Tất cả đều CHỈ CỘNG THÊM (CREATE INDEX), không đụng dữ liệu/constraint hiện có — an toàn chạy trên môi
/// trường đang có dữ liệu. Idempotent qua bắt ORA-00955 (index đã tồn tại), cùng khuôn Migration0064.
/// </summary>
public class Migration0065_AddInfrastructureListIndexes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory,
            "CREATE INDEX IX_INFRASTRUCTURE_TYPE_DELETED ON INFRASTRUCTURE (INFRA_TYPE_ID, IsDeleted)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_INFRASTRUCTURE_PARENT_ID ON INFRASTRUCTURE (PARENT_ID)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_INFRASTRUCTURE_CODE_NORM ON INFRASTRUCTURE (UPPER(TRIM(CODE)))");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_PMIS_EQTYPE_MAPPING_LOOKUP ON PMIS_EQUIPMENT_TYPE_MAPPING (PmisMaLoaiTB, GridTypeId, IsDeleted)");

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
