using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm index hàm KHỚP ĐÚNG biểu thức thật sự dùng trong mọi câu tra cứu PMIS_CODE nóng nhất hệ thống —
/// gọi 1 lần/bản ghi PMIS ở MỌI lượt đồng bộ (InfrastructureRepository.UpsertFromPmisAsync,
/// InfrastructurePmisLookup.ResolveByPmisCodeAsync, EquipmentRepository.UpsertFromPmisAsync):
/// <c>WHERE UPPER(TRIM(PMIS_CODE)) = UPPER(TRIM(:PmisCode)) AND IsDeleted = 0 [AND StatusTransition IS NULL]</c>
///
/// Trước migration này, 2 index hàm hiện có trên PMIS_CODE ĐỀU KHÔNG khớp được biểu thức trên nên Oracle
/// KHÔNG thể dùng chúng cho các câu query này (buộc phải full table scan mỗi lần gọi):
/// - Migration0047/0048: index THƯỜNG trên cột PMIS_CODE thô — không phục vụ được biểu thức hàm số
///   UPPER(TRIM(...)) (Oracle không tự áp B-tree thường cho kết quả 1 hàm số trên cột đó).
/// - Migration0060 (UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE/UX_EQUIPMENTS_ACTIVE_PMIS_CODE): index hàm nhưng
///   xây trên biểu thức <c>CASE WHEN &lt;điều kiện sống&gt; THEN UPPER(TRIM(PMIS_CODE)) END</c> — Oracle chỉ
///   dùng được function-based index khi biểu thức trong WHERE khớp Y HỆT cú pháp đã đăng ký (kể cả cấu
///   trúc CASE), mà câu query lại viết UPPER(TRIM(PMIS_CODE)) TRẦN + IsDeleted/StatusTransition là điều
///   kiện AND riêng — khác cấu trúc, không khớp.
///
/// Giải pháp: thêm index hàm THƯỜNG (không UNIQUE, không CASE) trên đúng biểu thức
/// <c>UPPER(TRIM(PMIS_CODE))</c> — khớp chính xác vế trái của mọi câu WHERE ở trên. Oracle range-scan theo
/// khoá này rồi tự lọc IsDeleted/StatusTransition trên các dòng trả về (thường 0-1 dòng nhờ unique index
/// Migration0060 đã đảm bảo không có ≥2 dòng "sống" trùng mã) — nhanh hơn full table scan hàng chục nghìn
/// lần/lượt đồng bộ đầy đủ. KHÔNG thay thế/đụng tới 2 index cũ (Migration0047/0048/0060 vẫn giữ nguyên vai
/// trò ràng buộc UNIQUE + phục vụ các câu query khác nếu có) — migration này chỉ CỘNG THÊM, không rủi ro
/// dữ liệu (CREATE INDEX không đụng constraint/dữ liệu hiện có).
/// </summary>
public class Migration0064_AddNormalizedPmisCodeIndexes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory,
            "CREATE INDEX IX_INFRASTRUCTURE_PMIS_CODE_NORM ON INFRASTRUCTURE (UPPER(TRIM(PMIS_CODE)))");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_EQUIPMENTS_PMIS_CODE_NORM ON EQUIPMENTS (UPPER(TRIM(PMIS_CODE)))");

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
