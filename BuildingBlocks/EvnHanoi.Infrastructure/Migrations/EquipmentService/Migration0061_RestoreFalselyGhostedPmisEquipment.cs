using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Khôi phục thiết bị bị đánh oan StatusTransition=0 ("Đã chuyển TBA") do bug PMIS_CODE lệch chuẩn
/// (khoảng trắng/hoa-thường) trước khi UPPER(TRIM(...)) được thêm vào UpsertFromPmisAsync (xem
/// Migration0059/0060): 1 trạm/đường dây bị tạo trùng bản ghi INFRASTRUCTURE do lệch mã, khiến
/// PMIS_SYNC hiểu nhầm CẢ trạm đã "chuyển đi" và ghost hoá thiết bị hàng loạt — đã gặp thật trên
/// production (1 trạm hiện trống trơn "Không có thiết bị nào thuộc trạm này" dù PMIS vẫn báo đã
/// đồng bộ, trong khi StatusTransition=0 vẫn giữ nguyên INFRASTRUCTURE_ID gốc ĐÚNG của thiết bị).
///
/// CHỈ khôi phục khi CẢ 3 điều kiện sau đúng (an toàn, không đụng tới chuyển TBA THẬT do người dùng
/// thao tác tay qua EquipmentController — luồng đó ghi ModifiedBy = tên user, không phải "PMIS_SYNC"):
/// 1. ModifiedBy = 'PMIS_SYNC' — chỉ ghost do chính lượt đồng bộ tự động gây ra (xem
///    EquipmentRepository.UpsertFromPmisAsync/CloneForInfrastructureTransferAsync).
/// 2. Không còn bản ghi "sống" nào khác trùng PMIS_CODE (đã chuẩn hoá UPPER/TRIM) — nếu còn, nghĩa
///    là có 1 bản "thay thế" đang thật sự tồn tại ở nơi khác (có thể là chuyển trạm thật, hoặc trạm
///    trùng lặp chưa được admin gộp) — KHÔNG tự ý khôi phục, để tránh vi phạm
///    UX_EQUIPMENTS_ACTIVE_PMIS_CODE (Migration0060) và tránh tạo thiết bị "sống" ở 2 nơi.
/// 3. Không còn bản ghi "sống" nào khác trùng (INFRASTRUCTURE_ID, Code) tại đúng trạm sẽ khôi phục về
///    — tránh vi phạm UX_EQUIPMENTS_ACTIVE_INFRA_CODE (Migration0059).
///
/// Các trạm/đường dây bị trùng bản ghi INFRASTRUCTURE (nếu còn) và các thiết bị KHÔNG thoả 2 điều
/// kiện an toàn trên (tức đang có bản "sống" ở nơi khác) migration này CỐ Ý bỏ qua — cần admin xác
/// định bản ghi INFRASTRUCTURE nào là "chính", gộp/xoá mềm bản trùng thủ công (xem ghi chú vận hành
/// trong Migration0060) trước khi xử lý tiếp các thiết bị còn lại.
/// </summary>
public class Migration0061_RestoreFalselyGhostedPmisEquipment : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();
        cmd.CommandText = @"
            UPDATE EQUIPMENTS e
            SET StatusTransition = NULL,
                ModifiedBy = 'MIGRATION_0061_RESTORE_GHOST',
                ModifiedDate = SYSTIMESTAMP
            WHERE e.StatusTransition = 0
              AND e.IsDeleted = 0
              AND e.ModifiedBy = 'PMIS_SYNC'
              AND e.PMIS_CODE IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM EQUIPMENTS e2
                  WHERE UPPER(TRIM(e2.PMIS_CODE)) = UPPER(TRIM(e.PMIS_CODE))
                    AND e2.IsDeleted = 0
                    AND e2.StatusTransition IS NULL
                    AND e2.Id <> e.Id
              )
              AND NOT EXISTS (
                  SELECT 1 FROM EQUIPMENTS e3
                  WHERE e3.INFRASTRUCTURE_ID = e.INFRASTRUCTURE_ID
                    AND e3.Code = e.Code
                    AND e3.IsDeleted = 0
                    AND e3.StatusTransition IS NULL
                    AND e3.Id <> e.Id
              )";
        cmd.ExecuteNonQuery();

        return string.Empty;
    }
}
