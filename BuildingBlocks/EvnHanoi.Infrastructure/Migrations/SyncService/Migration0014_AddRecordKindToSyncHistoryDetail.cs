using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bổ sung cột RECORD_KIND vào SYNC_HISTORY_DETAIL — phân loại 1 dòng chi tiết là INFRASTRUCTURE
/// (Trạm/Đường dây), EQUIPMENT (Thiết bị) hay DOCUMENT (tài liệu đính kèm).
///
/// LÝ DO: SyncDocumentsRotatingAsync (xem PmisScheduledSyncJob) cố ý dùng CHUNG SyncHistoryId với lượt
/// Trạm/Đường dây đang chạy (để gộp 1 kết quả tổng — total/success/failed/warnings), nhưng
/// SYNC_HISTORY_DETAIL trước đây không có cột nào phân biệt "dòng Trạm/Đường dây thật" (SourceName = tên
/// trạm) với "dòng tài liệu đính kèm" (SourceName = tên FILE, vd "OTPD-51096_...jpg") — cả 2 cùng nằm
/// trong cùng 1 bảng, cùng SyncHistoryId. Modal "Danh sách bản ghi đã đồng bộ" ở FE (API GET
/// {historyId}/items không lọc gì cả) vì vậy hiển thị lẫn tên file tài liệu vào tab "Trạm biến áp" —
/// phát hiện thật 2026-10-01.
///
/// KHÔNG backfill dữ liệu lịch sử cũ — NULL = "trước khi có phân loại", API/FE coi NULL như thuộc tab
/// chính đang xem (xem SyncHistoryRepository.GetDetailsPagedAsync) để không làm biến mất dữ liệu cũ, chỉ
/// dữ liệu MỚI từ nay mới được gán đúng và lọc đúng.
/// </summary>
public class Migration0014_AddRecordKindToSyncHistoryDetail : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = "ALTER TABLE SYNC_HISTORY_DETAIL ADD RECORD_KIND VARCHAR2(20) NULL";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
