using System;

namespace EvnHanoi.Infrastructure.Security;

/// <summary>
/// Attribute đánh dấu API/Controller bỏ qua việc quét tự động phân quyền hạt mịn.
/// Thường dùng cho các API nội bộ, API test hoặc các API dùng chung không gán menu.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class BypassPermissionScanAttribute : Attribute
{
}
