using System;

namespace EvnHanoi.Infrastructure.Security;

/// <summary>
/// Đánh dấu API engine quy trình (WorkflowController).
/// Không đăng ký ma trận quyền động — service này không có menu riêng trên portal.
/// Vẫn yêu cầu JWT ([Authorize]); kiểm soát truy cập qua logic nghiệp vụ trong WorkflowEngineService
/// (vai trò bước duyệt, task đang chờ, admin...).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class WorkflowEngineApiAttribute : Attribute
{
}
