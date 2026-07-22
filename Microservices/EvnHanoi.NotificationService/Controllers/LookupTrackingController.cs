using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

[ApiController]
[Route("api/v1/notification/lookup-tracking")]
public class LookupTrackingController : ControllerBase
{
    private readonly ILookupTrackingRepository _repository;

    public LookupTrackingController(ILookupTrackingRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Cộng dồn 1 lượt tra cứu hồ sơ/tài liệu theo ngày (menu Tìm kiếm fulltext, Tra cứu hồ sơ thiết bị,
    /// Tra cứu Trạm biến áp) — dùng cho báo cáo REPORT_DOSSIER_MOST_VIEWED. Không phải mutation nghiệp vụ
    /// (không tạo/sửa/xóa dữ liệu nghiệp vụ) — mọi user đã đăng nhập và đã xem được item đó qua tính năng
    /// tra cứu đều được phép ghi nhận, nên bypass permission động thay vì gắn quyền riêng cho một hành vi
    /// đếm lượt xem ngầm.
    /// POST /api/v1/notification/lookup-tracking
    /// </summary>
    [HttpPost]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> RecordView([FromBody] RecordLookupViewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DossierId))
            return BadRequest(new { message = "DossierId không được để trống" });

        if (!LookupViewEntityTypes.IsValid(request.EntityType))
            return BadRequest(new { message = "EntityType không hợp lệ" });

        await _repository.RecordViewAsync(request.EntityType, request.DossierId);

        return NoContent();
    }
}
