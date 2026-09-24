using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Sửa DEFAULT sai của cột EQUIPMENTS.STATUSTRANSITION — phát hiện live trên Oracle dev khi test batch-
/// prefetch (audit PMIS 2026-09-24, Giai đoạn 2): cột này hiện có DEFAULT '0' ở tầng DB, trong khi
/// Migration0039_AddStatusTransitionToEquipments.cs khai báo ĐÚNG "NUMBER(1) NULL" KHÔNG có DEFAULT nào —
/// giá trị DEFAULT '0' hiện tại KHÔNG đến từ bất kỳ migration nào trong repo, nhiều khả năng bị ALTER tay
/// ngoài luồng migration (cùng loại nghi vấn với script "PMIS_SYNC_CLEANUP" thất lạc đã ghi nhận trong
/// project memory pmis_sync_equipment_orphan_null_infra_id).
///
/// StatusTransition = 0 nghĩa là "Đã chuyển TBA" (xem EquipmentSqlFilters.NotTransferredAway,
/// EquipmentRepository.UpsertFromPmisAsync) — NULL nghĩa là "chưa từng chuyển". Với DEFAULT '0' sai, MỌI
/// INSERT không set cột này tường minh (VD nhánh tạo mới thiết bị trong
/// EquipmentRepository.UpsertFromPmisAsync — không có STATUSTRANSITION trong danh sách cột INSERT) sẽ tự
/// động bị gán 0 thay vì NULL. Hậu quả tái hiện được trực tiếp:
///   1. Thiết bị mới tạo bị MỌI bộ lọc UI coi là "đã chuyển đi" (EquipmentSqlFilters.NotTransferredAway),
///      biến mất khỏi danh sách thiết bị dù sync báo "Thành công".
///   2. Lượt sync KẾ TIẾP cho cùng PMIS_CODE không tìm lại được bản ghi vừa tạo (điều kiện tra cứu
///      "existing" luôn có AND StatusTransition IS NULL) → tạo THÊM 1 bản ghi trùng — lặp lại vô hạn mỗi
///      lượt đồng bộ.
/// Đây rất có thể là 1 phần nguyên nhân của hiện tượng "thiết bị đồng bộ về nhưng không hiển thị trên hệ
/// thống" đã điều tra trên production (210.245.84.38) trước đó trong cùng đợt audit này.
///
/// Migration này CHỈ sửa DEFAULT cho các lần INSERT SAU NÀY — KHÔNG backfill dữ liệu cũ đã bị ảnh hưởng
/// (các dòng EQUIPMENTS hiện có StatusTransition=0 dù thực ra chưa từng chuyển TBA thật). Backfill (nếu
/// cần) nên làm ở 1 migration RIÊNG có điều kiện lọc cẩn thận (phân biệt "đã chuyển TBA thật" — có
/// ModifiedBy/lịch sử tương ứng — với "bị gán oan do DEFAULT sai"), giống cách Migration0061 đã làm cho sự
/// cố PMIS_CODE lệch chuẩn trước đó — KHÔNG làm ở đây để tránh gộp 2 việc rủi ro khác nhau vào 1 script.
/// </summary>
public class Migration0066_FixEquipmentsStatusTransitionDefault : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        // ALTER TABLE ... MODIFY DEFAULT tự thân đã idempotent (chạy lại nhiều lần không lỗi, không đổi
        // dữ liệu hiện có) — không cần bắt lỗi ORA-xxxxx như các migration ADD COLUMN/CREATE INDEX khác.
        command.CommandText = "ALTER TABLE EQUIPMENTS MODIFY (StatusTransition DEFAULT NULL)";
        command.ExecuteNonQuery();

        return string.Empty;
    }
}
