using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bổ sung cột DOCUMENT_SYNC_CURSOR vào SYNC_CONFIG — cursor RIÊNG (độc lập với SYNC_CURSOR ở
/// Migration0011, vốn chỉ phục vụ phân trang dữ liệu chính) cho việc xoay vòng ưu tiên đồng bộ TÀI LIỆU
/// đính kèm/ảnh QR của Trạm biến áp/Đường dây.
///
/// TRƯỚC ĐÂY: đồng bộ tài liệu chạy LỒNG trong vòng lặp phân trang PMIS (SyncInfrastructureAsync gọi
/// SyncDocumentsForOwnerAsync ngay khi 1 trang vừa upsert xong) — ngân sách MaxDocumentSyncCallsPerRun
/// (2000 owner/lượt) luôn cạn ở ĐÚNG cùng ~2000 Trạm/Đường dây ĐẦU danh sách mỗi lượt (thứ tự PMIS trả
/// về ổn định), khiến các owner còn lại (có thể hàng chục nghìn) KHÔNG BAO GIỜ được đồng bộ tài liệu —
/// khác EQUIPMENT (đã có rotation qua SYNC_CURSOR từ trước) vì danh sách "cha" của EQUIPMENT lấy từ
/// chính DB mình (rẻ, fetch được toàn bộ để xoay vòng trước khi lặp), còn danh sách Trạm/Đường dây tới
/// từ live pagination PMIS (không thể rẻ để fetch-toàn-bộ-rồi-xoay-vòng theo cùng cách).
///
/// Giải pháp: tách đồng bộ tài liệu Trạm/Đường dây ra thành 1 PASS RIÊNG chạy SAU vòng lặp phân trang
/// chính (xem PmisScheduledSyncJob.SyncDocumentsRotatingAsync) — fetch toàn bộ owner ĐÃ đồng bộ từ DB
/// mình (InfrastructureRepository.GetSyncedPmisCodesByTypeAsync, rẻ, có index), xoay vòng theo
/// DOCUMENT_SYNC_CURSOR giống hệt cách EQUIPMENT đã làm với SYNC_CURSOR.
///
/// Ý nghĩa: mã PMIS (PmisCode) của Trạm/Đường dây nơi ngân sách MaxDocumentSyncCallsPerRun bị dùng hết ở
/// lượt trước — lượt sau xoay vòng để bắt đầu NGAY SAU mã này. NULL = lượt trước hoàn tất trọn vẹn (mọi
/// owner đều được đồng bộ tài liệu), lượt sau bắt đầu lại từ đầu danh sách.
/// </summary>
public class Migration0013_AddDocumentSyncCursorToSyncConfig : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = "ALTER TABLE SYNC_CONFIG ADD DOCUMENT_SYNC_CURSOR VARCHAR2(200 CHAR) NULL";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
