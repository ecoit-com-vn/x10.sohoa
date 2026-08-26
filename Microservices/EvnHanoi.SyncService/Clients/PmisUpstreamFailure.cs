using Polly.CircuitBreaker;
using Polly.Timeout;

namespace EvnHanoi.SyncService.Clients;

/// <summary>
/// Nhận diện lỗi do PHÍA PMIS (không phải lỗi hệ thống mình) để controller trả 503 kèm thông báo tiếng
/// Việt, thay vì để nguyên exception Polly bay ra ngoài — trước đây màn "Đồng bộ thủ công" hiện thẳng
/// stack trace `Polly.CircuitBreaker.BrokenCircuitException` cho người dùng cuối.
/// </summary>
public static class PmisUpstreamFailure
{
    public static bool Matches(Exception ex) => ex switch
    {
        BrokenCircuitException => true,       // đã lỗi liên tục, circuit breaker đang mở
        TimeoutRejectedException => true,     // quá thời gian chờ của policy
        HttpRequestException => true,         // không kết nối được / lỗi HTTP tầng vận chuyển
        TaskCanceledException => true,        // timeout của HttpClient
        _ => false
    };

    public static string UserMessage(Exception ex) => ex switch
    {
        BrokenCircuitException =>
            "Hệ thống PMIS đang lỗi liên tục nên kết nối đã tạm ngắt để tránh gọi dồn. " +
            "Vui lòng thử lại sau khoảng 30 giây.",
        TimeoutRejectedException or TaskCanceledException =>
            "Hệ thống PMIS phản hồi quá chậm (quá thời gian chờ). Vui lòng thử lại sau.",
        _ =>
            "Không gọi được hệ thống PMIS. Kiểm tra lại URL trong 'Cấu hình kết nối PMIS' hoặc trạng thái " +
            "của PMIS rồi thử lại."
    };
}
