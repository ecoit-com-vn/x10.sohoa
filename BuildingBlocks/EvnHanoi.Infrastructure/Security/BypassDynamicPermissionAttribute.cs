using System;

namespace EvnHanoi.Infrastructure.Security;

/// <summary>
/// Attribute đánh dấu API bỏ qua kiểm tra quyền động qua DynamicPermissionFilter.
/// Chỉ yêu cầu xác thực JWT chuẩn ([Authorize]).
/// Thường dùng cho các API lõi dùng chung (như Lấy profile cá nhân, lookup dữ liệu).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class BypassDynamicPermissionAttribute : Attribute
{
}
