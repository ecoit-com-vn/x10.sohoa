using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Khôi phục thiết bị "SINH RA ĐÃ LÀ HỒN MA": EQUIPMENTS.STATUSTRANSITION từng có DEFAULT '0' ở mức DB (không
/// nằm trong migration nào, xem Migration0066) nên MỌI thiết bị PMIS_SYNC tạo mới trước khi 0066 chạy đều
/// bị gắn "Đã chuyển TBA" ngay lúc INSERT dù chưa từng chuyển đi đâu — vô hình với mọi danh sách (lọc
/// StatusTransition IS NULL) và với bước tra cứu của UpsertFromPmisAsync (nên lượt đồng bộ sau cứ INSERT
/// lại, đụng ORA-00001 — xem Migration0072). Migration0066 chỉ sửa DEFAULT cho dòng MỚI, không chữa dòng cũ.
///
/// Số liệu thật trên production (2026-10-01): 133.904 dòng StatusTransition=0, TOÀN BỘ đều CreatedBy =
/// 'PMIS_SYNC', ModifiedBy/ModifiedDate = NULL (chưa từng được sửa lần nào), EQUIPMENT_TRANSFER_HISTORY có
/// 0 dòng — tức không có ca chuyển trạm thật nào. UAT: 56.884 dòng tương tự.
///
/// Không dùng được Migration0061 vì nó đòi ModifiedBy = 'PMIS_SYNC' (chuyển trạm thật do PMIS_SYNC gây ra
/// luôn ghi ModifiedBy/ModifiedDate) — các dòng sinh-ra-đã-ghost này chưa từng được sửa nên ModifiedBy NULL.
///
/// ĐIỀU KIỆN KHÔI PHỤC (đủ cả 5, an toàn — không đụng chuyển TBA thật):
/// 1. StatusTransition = 0, IsDeleted = 0, CreatedBy = 'PMIS_SYNC', ModifiedBy IS NULL, ModifiedDate IS NULL
///    — chưa từng bị sửa (chuyển thật luôn để lại ModifiedBy/ModifiedDate, xem EquipmentRepository
///    UpsertFromPmisAsync/EquipmentController.CreateFromById).
/// 2. Không xuất hiện làm SourceEquipmentId trong EQUIPMENT_TRANSFER_HISTORY (đai an toàn thứ hai).
/// 3. INFRASTRUCTURE_ID và CODE đều khác NULL (dòng mồ côi để nguyên — NULL một phần trong khoá unique
///    của Oracle vẫn tính là trùng, khó đoán).
/// 4. Chưa có dòng "sống" nào cùng (INFRASTRUCTURE_ID, CODE) hoặc cùng PMIS_CODE (UPPER/TRIM) — nếu có,
///    thiết bị đã được sync tạo lại thành dòng mới, KHÔNG khôi phục (tránh vi phạm 2 unique index hàm của
///    Migration0059/0060 và tránh 2 bản "sống" cùng 1 thiết bị). Trên production: 2.435 dòng rơi vào đây,
///    để nguyên ghost.
/// 5. Trong nhóm các ghost cùng (INFRASTRUCTURE_ID, CODE) hoặc cùng PMIS_CODE, chỉ chọn ĐÚNG 1 dòng
///    (CreatedAt mới nhất) bằng ROW_NUMBER — nếu để cả 2 cùng khôi phục thì tự đụng độ lẫn nhau. 2 tiêu chí
///    xếp hạng chọn ra 2 dòng khác nhau thì bỏ hẳn nhóm đó (an toàn hơn đoán). Dòng PMIS_CODE NULL được
///    xếp hạng riêng từng dòng (COALESCE theo Id), không gộp chung vào 1 nhóm NULL.
///
/// CHIA LÔ (BatchSize dòng/lần, tính lại điều kiện sau mỗi lô) để không giữ khoá/undo của hàng trăm nghìn dòng
/// trong 1 giao dịch khi các job đồng bộ vẫn đang chạy song song; gặp ORA-00001 (job đồng bộ vừa tạo dòng
/// "sống" trùng giữa chừng) thì tính lại lô và thử lại, tối đa MaxRetriesPerBatch lần. Mỗi dòng được khôi phục
/// tự gắn ModifiedBy = 'MIGRATION_0073_RESTORE_BORN_GHOST' nên ROLLBACK chính xác được (xem file .sql thủ công).
///
/// Idempotent: chạy lại chỉ chọn các dòng còn thoả điều kiện (đã khôi phục thì không còn StatusTransition=0).
/// LƯU Ý sau khi chạy: chỉ mục Elasticsearch của thiết bị (NotificationService) không tự biết các dòng vừa
/// được khôi phục — cần resync thiết bị lên ES để tìm kiếm thấy chúng.
/// </summary>
public class Migration0073_RestoreBornGhostEquipment : IScript
{
    private const int BatchSize = 10000;
    private const int MaxRetriesPerBatch = 5;
    private const int MaxBatches = 1000; // chặn vòng lặp vô hạn nếu có lỗi logic ngoài dự kiến.

    private static readonly string RestoreBatchSql = $@"
        UPDATE EQUIPMENTS
        SET StatusTransition = NULL,
            ModifiedBy = 'MIGRATION_0073_RESTORE_BORN_GHOST',
            ModifiedDate = SYSTIMESTAMP
        WHERE Id IN (
            SELECT Id FROM (
                SELECT x.Id FROM (
                    SELECT g.Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY COALESCE(UPPER(TRIM(g.PMIS_CODE)), g.Id)
                               ORDER BY g.CreatedAt DESC, g.Id DESC) AS rn_code,
                           ROW_NUMBER() OVER (
                               PARTITION BY g.INFRASTRUCTURE_ID, g.Code
                               ORDER BY g.CreatedAt DESC, g.Id DESC) AS rn_infra_code
                    FROM EQUIPMENTS g
                    WHERE g.StatusTransition = 0
                      AND g.IsDeleted = 0
                      AND g.CreatedBy = 'PMIS_SYNC'
                      AND g.ModifiedBy IS NULL
                      AND g.ModifiedDate IS NULL
                      AND g.INFRASTRUCTURE_ID IS NOT NULL
                      AND g.Code IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM EQUIPMENT_TRANSFER_HISTORY h WHERE h.SourceEquipmentId = g.Id)
                      AND NOT EXISTS (
                          SELECT 1 FROM EQUIPMENTS l
                          WHERE l.IsDeleted = 0 AND l.StatusTransition IS NULL
                            AND l.INFRASTRUCTURE_ID = g.INFRASTRUCTURE_ID AND l.Code = g.Code)
                      AND (g.PMIS_CODE IS NULL OR NOT EXISTS (
                          SELECT 1 FROM EQUIPMENTS l2
                          WHERE l2.IsDeleted = 0 AND l2.StatusTransition IS NULL
                            AND UPPER(TRIM(l2.PMIS_CODE)) = UPPER(TRIM(g.PMIS_CODE))))
                ) x
                WHERE x.rn_code = 1 AND x.rn_infra_code = 1
                ORDER BY x.Id
            )
            WHERE ROWNUM <= {BatchSize}
        )";

    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        long total = 0;
        for (var batch = 1; batch <= MaxBatches; batch++)
        {
            var restored = RestoreOneBatch(dbCommandFactory);
            if (restored == 0) break;

            total += restored;
            Console.WriteLine($"Migration0073: lô {batch} khôi phục {restored} thiết bị (tổng {total}).");
        }

        Console.WriteLine($"Migration0073: hoàn tất, đã khôi phục {total} thiết bị sinh ra đã là hồn ma.");
        return string.Empty;
    }

    private static int RestoreOneBatch(Func<IDbCommand> dbCommandFactory)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var command = dbCommandFactory();
            try
            {
                command.CommandText = RestoreBatchSql;
                return command.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase)
                                       && attempt < MaxRetriesPerBatch)
            {
                // Job đồng bộ vừa tạo dòng "sống" trùng đúng 1 dòng trong lô — tính lại lô (điều kiện
                // NOT EXISTS giờ đã loại dòng đó) rồi thử lại.
                Console.WriteLine($"Migration0073: ORA-00001 giữa chừng (lần {attempt}), tính lại lô và thử lại.");
            }
        }
    }
}
