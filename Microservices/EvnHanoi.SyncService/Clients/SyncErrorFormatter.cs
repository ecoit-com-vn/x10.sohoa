using System.Net.Http;
using Polly;
using Polly.CircuitBreaker;

namespace EvnHanoi.SyncService.Clients;

/// <summary>
/// Định dạng chi tiết exception để lưu vào SYNC_HISTORY/SYNC_HISTORY_DETAIL/PMIS_API_CALL_LOG.ErrorMessage
/// (NVARCHAR2(2000)). Ưu tiên hiển thị NGUYÊN NHÂN GỐC THẬT thay vì lớp vỏ bọc của Polly
/// (BrokenCircuitException: "The circuit is now open and is not allowing calls."/TimeoutRejectedException
/// — chỉ nói "không gọi được" chứ không nói kết nối tới đâu, vì sao).
///
/// Có 2 dạng nguyên nhân gốc hoàn toàn khác nhau cần xử lý riêng (đã kiểm chứng bằng log thật + reflection
/// trên Polly 7.2.4):
/// 1. Circuit mở do EXCEPTION (mất kết nối, DNS lỗi...) — BrokenCircuitException (non-generic) có
///    InnerException là exception thật (vd. HttpRequestException: "Resource temporarily unavailable
///    (demogwlan.evnhanoi.vn:443)" — .NET gắn thêm host:port vào message ở lớp NÀY, không có ở
///    SocketException bên dưới, nên dừng lại đây, KHÔNG unwrap tiếp).
/// 2. Circuit mở do PMIS trả HTTP status lỗi (500/503/408 — HandleTransientHttpError() coi status đó là
///    "fault" dù không có exception nào được throw) — Polly tạo BrokenCircuitException&lt;HttpResponseMessage&gt;
///    (GENERIC, kế thừa từ bản non-generic) với InnerException = null nhưng có property Result chứa chính
///    HttpResponseMessage lỗi đó — đây là dạng phổ biến nhất trên production thực tế (xem
///    Logs/log-20260829.txt), nếu chỉ unwrap theo InnerException sẽ luôn ra tay không (message vẫn
///    y nguyên "The circuit is now open..."), phải đọc riêng property Result.
///
/// KHÔNG còn kèm stack trace kỹ thuật trong thông báo hiển thị cho người dùng — stack trace đầy đủ vẫn
/// được ghi qua Serilog (Log.Error(ex, ...)) ở nơi gọi, chỉ không lưu vào cột hiển thị trên UI.
/// </summary>
public static class SyncErrorFormatter
{
    private const int MaxLength = 1900; // chừa margin cho cột NVARCHAR2(2000)
    private const int MaxLengthShort = 300; // dùng khi nhiều dòng lỗi bị nối lại (Take(5) rồi join)

    /// <summary>Bỏ qua các lớp vỏ bọc "không thực sự gọi/không xong" của Polly (circuit breaker mở,
    /// timeout policy...) để lấy exception THẬT đầu tiên bên dưới — nơi message còn giữ chi tiết kết nối
    /// (host:port, lý do socket) do PMIS/hạ tầng mạng trả về. Dừng lại NGAY khi gặp
    /// BrokenCircuitException&lt;HttpResponseMessage&gt; có Result — trường hợp đó không unwrap theo
    /// InnerException được (luôn null), phải xử lý riêng ở Describe().</summary>
    private static Exception MeaningfulCause(Exception ex)
    {
        var current = ex;
        while (current is ExecutionRejectedException
               && current is not BrokenCircuitException<HttpResponseMessage> { Result: not null }
               && current.InnerException != null)
        {
            current = current.InnerException;
        }
        return current;
    }

    /// <summary>Diễn giải 1 exception thành chuỗi dễ hiểu — riêng BrokenCircuitException&lt;HttpResponseMessage&gt;
    /// đọc thẳng status code + URI từ Result thay vì dùng Message chung ("The circuit is now open...").</summary>
    private static string Describe(Exception ex)
    {
        if (ex is BrokenCircuitException<HttpResponseMessage> { Result: { } response })
        {
            var status = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
            var uri = response.RequestMessage?.RequestUri;
            return uri == null ? $"PMIS trả về {status}" : $"PMIS trả về {status} ({uri})";
        }

        return $"{ex.GetType().Name}: {ex.Message}";
    }

    /// <summary>Định dạng đầy đủ — dùng khi ErrorMessage chỉ lưu đúng 1 lỗi (không bị nối với lỗi khác).
    /// Hiện cả loại lỗi ngoài cùng (ngữ cảnh: đang bị chặn do circuit breaker/timeout...) LẪN nguyên nhân
    /// gốc bên trong, để vừa biết PMIS đang ở trạng thái gì vừa biết vì sao.</summary>
    public static string Format(Exception ex)
    {
        var cause = MeaningfulCause(ex);
        var result = ReferenceEquals(ex, cause)
            ? Describe(ex)
            : $"{ex.GetType().Name}: {ex.Message} ---> Nguyên nhân gốc: {Describe(cause)}";

        return result.Length > MaxLength ? result[..MaxLength] + "…" : result;
    }

    /// <summary>Định dạng ngắn gọn — chỉ lấy NGUYÊN NHÂN GỐC (không kèm lớp vỏ bọc ngoài, không kèm stack
    /// trace) — dùng cho lỗi từng trang khi nhiều dòng bị nối lại bằng "; " (xem
    /// PmisScheduledSyncJob.PushPageAsync), tránh vượt giới hạn cột khi ghép chung nhiều lỗi.</summary>
    public static string FormatShort(Exception ex)
    {
        var message = Describe(MeaningfulCause(ex));
        return message.Length > MaxLengthShort ? message[..MaxLengthShort] + "…" : message;
    }
}
