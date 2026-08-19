namespace EvnHanoi.EquipmentService.Core.Interfaces;

/// <summary>
/// Client gọi sang IdentityService cho các thông tin người dùng hiện tại mà JWT claims không có
/// sẵn (ví dụ SsoNsId — EVN HRMS ns_ID, cần cho tích hợp ký số). Dùng HttpClient đặt tên
/// "IdentityService" (đã đăng ký sẵn ở Program.cs) kèm TokenRelayHandler để forward Bearer token
/// của request gốc — IdentityService tự xác định "current user" từ token đó.
/// </summary>
public interface IIdentityServiceClient
{
    /// <summary>Lấy SsoNsId (EVN HRMS ns_ID) của người dùng hiện tại — null nếu chưa cấu hình hoặc lỗi.</summary>
    Task<string?> GetCurrentUserSsoNsIdAsync(CancellationToken cancellationToken = default);
}
