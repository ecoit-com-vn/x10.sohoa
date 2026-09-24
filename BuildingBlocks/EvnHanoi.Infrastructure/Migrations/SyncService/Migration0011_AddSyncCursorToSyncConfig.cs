using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bổ sung cột SYNC_CURSOR vào SYNC_CONFIG — điểm "tiếp tục" khi 1 lượt đồng bộ tự động (Trạm biến áp/
/// Đường dây/Thiết bị) dừng giữa chừng vì chạm giới hạn an toàn (PmisPaging.MaxTotalRecordsPerRun, hoặc
/// ngân sách gọi PMIS thật ChiTietThietBi/QR/tài liệu đính kèm) — TRƯỚC ĐÂY mọi vòng lặp phân trang luôn
/// bắt đầu lại từ skip=0 ở MỌI lượt chạy, nên nếu tổng số bản ghi thật vượt giới hạn an toàn, phần dữ liệu
/// sau vị trí giới hạn KHÔNG BAO GIỜ được xử lý (PMIS trả cùng thứ tự ổn định mỗi lượt) — xem
/// PmisScheduledSyncJob. Ý nghĩa cột tuỳ theo ObjectType (chuỗi thô, không ràng buộc kiểu ở tầng DB):
/// - SUBSTATION/TRANSMISSION_LINE: số nguyên "skip" (vị trí trang) để tiếp tục phân trang từ đó thay vì 0.
/// - EQUIPMENT: mã PMIS (PmisCode) của Trạm/Đường dây cha nơi ngân sách gọi PMIS thật (ChiTietThietBi/QR)
///   bị dùng hết ở lượt trước — lượt sau xoay vòng danh sách cha để bắt đầu NGAY SAU mã này, đảm bảo các
///   cha khác nhau lần lượt được ưu tiên ngân sách thay vì luôn đúng nhóm đầu danh sách mỗi lượt.
/// NULL nghĩa là lượt trước hoàn tất trọn vẹn (không chạm giới hạn nào) — lượt sau bắt đầu lại từ đầu.
/// </summary>
public class Migration0011_AddSyncCursorToSyncConfig : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = "ALTER TABLE SYNC_CONFIG ADD SYNC_CURSOR VARCHAR2(200 CHAR) NULL";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
