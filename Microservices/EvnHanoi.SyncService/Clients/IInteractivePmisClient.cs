namespace EvnHanoi.SyncService.Clients;

/// <summary>
/// Cùng hợp đồng với <see cref="IPmisClient"/> nhưng gọi qua HttpClient "PMIS-Interactive" (circuit
/// breaker riêng, tách khỏi "PMIS" — xem PmisClient) — dùng cho các API tra cứu/tìm kiếm tương tác mà
/// người dùng đang chờ trực tiếp trên màn hình: <see cref="Controllers.PmisLookupController"/> và bước
/// "Tìm kiếm" của <see cref="Controllers.PmisManualSyncController"/>. Đồng bộ nền (tự động theo lịch,
/// và bước "Lưu" của đồng bộ thủ công — cả 2 đều đi qua PmisSyncExecutionService) vẫn dùng
/// <see cref="IPmisClient"/> mặc định, không tách, vì bản chất đã là tác vụ nặng/chạy nền chấp nhận độ
/// trễ, và việc tách circuit breaker ở đây không giải quyết vấn đề gì thêm.
/// </summary>
public interface IInteractivePmisClient : IPmisClient
{
}
