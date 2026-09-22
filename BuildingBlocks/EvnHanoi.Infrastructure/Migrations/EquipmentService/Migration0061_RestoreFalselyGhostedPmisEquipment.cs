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
/// LƯU Ý (phát hiện khi chạy tay lần đầu, gặp ORA-00001 trên UX_EQUIPMENTS_ACTIVE_PMIS_CODE): 2 điều
/// kiện NOT EXISTS ở trên chỉ loại trừ xung đột với các dòng đang SỐNG SẴN TỪ TRƯỚC — nếu 2 dòng
/// "hồn ma" KHÁC NHAU cùng trùng 1 PMIS_CODE (thiết bị bị ghost oan nhiều lần liên tiếp) đều không
/// xung đột với dòng sống nào, CẢ 2 sẽ được UPDATE cùng lúc trong 1 câu lệnh — sau khi update, cả 2
/// cùng "sống" với cùng mã, tự đụng độ NGAY VỚI NHAU. Oracle rollback toàn bộ statement khi gặp lỗi
/// này (không có gì bị ghi), nhưng vẫn cần loại trùng NGAY TRONG batch bằng ROW_NUMBER(): mỗi nhóm
/// PMIS_CODE (đã chuẩn hoá) và mỗi nhóm (INFRASTRUCTURE_ID, Code) chỉ chọn ĐÚNG 1 dòng — ưu tiên dòng
/// có ModifiedDate mới nhất (khả năng cao là lần ghost gần nhất/đúng nhất). Nếu 2 tiêu chí xếp hạng
/// (theo PMIS_CODE và theo Infra+Code) chọn ra 2 dòng KHÁC NHAU cho cùng 1 nhóm — bỏ qua hẳn nhóm đó
/// (an toàn hơn là đoán), để lại cho admin xử lý tay.
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
            WHERE e.Id IN (
                SELECT x.Id FROM (
                    SELECT c.Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY UPPER(TRIM(c.PMIS_CODE))
                               ORDER BY c.ModifiedDate DESC, c.Id DESC) AS rn_code,
                           ROW_NUMBER() OVER (
                               PARTITION BY c.INFRASTRUCTURE_ID, c.Code
                               ORDER BY c.ModifiedDate DESC, c.Id DESC) AS rn_infra_code
                    FROM EQUIPMENTS c
                    WHERE c.StatusTransition = 0
                      AND c.IsDeleted = 0
                      AND c.ModifiedBy = 'PMIS_SYNC'
                      AND c.PMIS_CODE IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM EQUIPMENTS e2
                          WHERE UPPER(TRIM(e2.PMIS_CODE)) = UPPER(TRIM(c.PMIS_CODE))
                            AND e2.IsDeleted = 0
                            AND e2.StatusTransition IS NULL
                            AND e2.Id <> c.Id
                      )
                      AND NOT EXISTS (
                          SELECT 1 FROM EQUIPMENTS e3
                          WHERE e3.INFRASTRUCTURE_ID = c.INFRASTRUCTURE_ID
                            AND e3.Code = c.Code
                            AND e3.IsDeleted = 0
                            AND e3.StatusTransition IS NULL
                            AND e3.Id <> c.Id
                      )
                ) x
                WHERE x.rn_code = 1 AND x.rn_infra_code = 1
            )";
        cmd.ExecuteNonQuery();

        return string.Empty;
    }
}
