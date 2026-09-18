using Polly;

namespace EvnHanoi.SyncService.Clients;

/// <summary>
/// Định dạng chi tiết exception để lưu vào SYNC_HISTORY/SYNC_HISTORY_DETAIL/PMIS_API_CALL_LOG.ErrorMessage
/// (NVARCHAR2(2000)). Ưu tiên hiển thị NGUYÊN NHÂN GỐC THẬT (vd. HttpRequestException: "Resource
/// temporarily unavailable (demogwlan.evnhanoi.vn:443)" — có kèm rõ host:port PMIS) thay vì lớp vỏ bọc
/// của Polly (BrokenCircuitException: "The circuit is now open and is not allowing calls."/
/// TimeoutRejectedException — chỉ nói "không gọi được" chứ không nói kết nối tới đâu, vì sao) — trước đây
/// FormatShort chỉ lấy đúng exception ngoài cùng nên người dùng chỉ thấy "BrokenCircuitException...".
/// Dừng lại ở exception THẬT đầu tiên (không phải Polly.ExecutionRejectedException — lớp cha chung của
/// BrokenCircuitException/TimeoutRejectedException, đã xác nhận qua reflection) chứ KHÔNG đi tiếp
/// xuống SocketException bên dưới — SocketException thường chỉ lặp lại message ngắn ("Resource
/// temporarily unavailable") mà KHÔNG còn host:port (HttpRequestException là nơi .NET gắn thêm host:port
/// vào message), đi quá sâu sẽ mất chính thông tin người dùng cần thấy. KHÔNG còn kèm stack trace kỹ
/// thuật trong thông báo hiển thị cho người dùng — stack trace đầy đủ vẫn được ghi qua Serilog
/// (Log.Error(ex, ...)) ở nơi gọi, chỉ không lưu vào cột hiển thị trên UI.
/// </summary>
public static class SyncErrorFormatter
{
    private const int MaxLength = 1900; // chừa margin cho cột NVARCHAR2(2000)
    private const int MaxLengthShort = 300; // dùng khi nhiều dòng lỗi bị nối lại (Take(5) rồi join)

    /// <summary>Bỏ qua các lớp vỏ bọc "không thực sự gọi/không xong" của Polly (circuit breaker mở,
    /// timeout policy...) để lấy exception THẬT đầu tiên bên dưới — nơi message còn giữ chi tiết kết nối
    /// (host:port, lý do socket) do PMIS/hạ tầng mạng trả về.</summary>
    private static Exception MeaningfulCause(Exception ex)
    {
        var current = ex;
        while (current is ExecutionRejectedException && current.InnerException != null)
        {
            current = current.InnerException;
        }
        return current;
    }

    /// <summary>Định dạng đầy đủ — dùng khi ErrorMessage chỉ lưu đúng 1 lỗi (không bị nối với lỗi khác).
    /// Hiện cả loại lỗi ngoài cùng (ngữ cảnh: đang bị chặn do circuit breaker/timeout...) LẪN nguyên nhân
    /// gốc bên trong, để vừa biết PMIS đang ở trạng thái gì vừa biết vì sao.</summary>
    public static string Format(Exception ex)
    {
        var cause = MeaningfulCause(ex);
        var result = ReferenceEquals(ex, cause)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"{ex.GetType().Name}: {ex.Message} ---> Nguyên nhân gốc: {cause.GetType().Name}: {cause.Message}";

        return result.Length > MaxLength ? result[..MaxLength] + "…" : result;
    }

    /// <summary>Định dạng ngắn gọn — chỉ lấy NGUYÊN NHÂN GỐC (không kèm lớp vỏ bọc ngoài, không kèm stack
    /// trace) — dùng cho lỗi từng trang khi nhiều dòng bị nối lại bằng "; " (xem
    /// PmisScheduledSyncJob.PushPageAsync), tránh vượt giới hạn cột khi ghép chung nhiều lỗi.</summary>
    public static string FormatShort(Exception ex)
    {
        var cause = MeaningfulCause(ex);
        var message = $"{cause.GetType().Name}: {cause.Message}";
        return message.Length > MaxLengthShort ? message[..MaxLengthShort] + "…" : message;
    }
}
