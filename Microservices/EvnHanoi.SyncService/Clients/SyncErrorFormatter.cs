using System.Text;

namespace EvnHanoi.SyncService.Clients;

/// <summary>
/// Định dạng chi tiết exception để lưu vào SYNC_HISTORY/SYNC_HISTORY_DETAIL.ErrorMessage
/// (NVARCHAR2(2000)) — trước đây chỉ lưu <c>ex.Message</c> (vd. "Response status code does not
/// indicate success: 500 (Internal Server Error)."), không đủ để biết nguyên nhân thật (gọi API nào,
/// lỗi ở đâu) mà không phải vào log pod đọc thủ công. Giờ gồm loại exception, message, exception lồng
/// bên trong (nếu có), và stack trace — cắt bớt cho vừa giới hạn cột.
/// </summary>
public static class SyncErrorFormatter
{
    private const int MaxLength = 1900; // chừa margin cho cột NVARCHAR2(2000)
    private const int MaxLengthShort = 300; // dùng khi nhiều dòng lỗi bị nối lại (Take(5) rồi join)

    /// <summary>Định dạng đầy đủ — dùng khi ErrorMessage chỉ lưu đúng 1 lỗi (không bị nối với lỗi khác).</summary>
    public static string Format(Exception ex)
    {
        var sb = new StringBuilder();
        var current = ex;
        var depth = 0;
        while (current != null && depth < 3)
        {
            if (depth > 0) sb.Append(" ---> ");
            sb.Append(current.GetType().Name).Append(": ").Append(current.Message);
            current = current.InnerException;
            depth++;
        }

        sb.Append(" | Stack: ").Append(string.IsNullOrEmpty(ex.StackTrace) ? "(không có stack trace)" : ex.StackTrace);

        var result = sb.ToString();
        return result.Length > MaxLength ? result[..MaxLength] + "…" : result;
    }

    /// <summary>Định dạng ngắn gọn (loại + message, không kèm stack trace) — dùng cho lỗi từng trang khi
    /// nhiều dòng bị nối lại bằng "; " (xem PmisScheduledSyncJob.PushPageAsync), tránh vượt giới hạn cột
    /// khi ghép chung nhiều lỗi.</summary>
    public static string FormatShort(Exception ex)
    {
        var message = $"{ex.GetType().Name}: {ex.Message}";
        return message.Length > MaxLengthShort ? message[..MaxLengthShort] + "…" : message;
    }
}
