using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Đưa MỌI tài liệu PMIS CHƯA có file về hàng đợi tải sạch: FILE_STATUS='PENDING', FILE_ATTEMPTS=0, FILE_NEXT_RETRY_AT=NULL,
/// FILE_LAST_ERROR=NULL.
///
/// BỐI CẢNH (2026-10): file tài liệu giờ tải theo MÃ tài liệu qua API cấu hình DOCUMENT_FILE_DOWNLOAD (xem SyncService
/// Migration0015), không còn theo URL lưu trong PMIS_DOCUMENT.FILE_URL. Hàng chục nghìn dòng đang mang lỗi/lịch thử lại của cách
/// cũ (404 do gateway chưa có route, HTTP 500, backoff dài, FAILED chờ 24 giờ, NO_URL do PMIS bỏ link) — không phản ánh việc tải
/// bằng API mới, nên reset để job thử lại ngay. Dòng đã có file (ObjectKey) và dòng đã xoá mềm không đụng tới.
///
/// Chia lô 10.000 (~295.000 dòng trên production) để không giữ undo/khoá lớn khi các job vẫn chạy; điều kiện chọn dòng loại các dòng
/// ĐÃ sạch nên vòng lặp tự dừng và migration idempotent (chạy lại: 0 dòng). Chỉ chạy 1 lần nhờ journal DbUp.
/// LƯU Ý: sau reset, toàn bộ ~295.000 dòng đủ điều kiện tải ngay khi admin bật DOCUMENT_FILE_DOWNLOAD — theo dõi tải lên PMIS.
/// </summary>
public class Migration0075_ResetPmisDocumentFileQueue : IScript
{
    private const int BatchSize = 10000;
    private const int MaxBatches = 200; // chặn vòng lặp vô hạn nếu có lỗi logic ngoài dự kiến.

    private static readonly string ResetBatchSql = $@"
        UPDATE PMIS_DOCUMENT
        SET FILE_STATUS = 'PENDING', FILE_ATTEMPTS = 0, FILE_NEXT_RETRY_AT = NULL, FILE_LAST_ERROR = NULL
        WHERE ROWID IN (
            SELECT ROWID FROM PMIS_DOCUMENT
            WHERE ObjectKey IS NULL AND IsDeleted = 0
              AND (FILE_STATUS <> 'PENDING' OR FILE_ATTEMPTS <> 0 OR FILE_NEXT_RETRY_AT IS NOT NULL OR FILE_LAST_ERROR IS NOT NULL)
              AND ROWNUM <= {BatchSize}
        )";

    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        long total = 0;
        for (var batch = 1; batch <= MaxBatches; batch++)
        {
            using var command = dbCommandFactory();
            command.CommandText = ResetBatchSql;
            var reset = command.ExecuteNonQuery();
            if (reset == 0) break;

            total += reset;
            Console.WriteLine($"Migration0075: lô {batch} đặt lại {reset} tài liệu (tổng {total}).");
        }

        Console.WriteLine($"Migration0075: hoàn tất, đã đặt lại {total} tài liệu chưa có file về hàng đợi.");
        return string.Empty;
    }
}
