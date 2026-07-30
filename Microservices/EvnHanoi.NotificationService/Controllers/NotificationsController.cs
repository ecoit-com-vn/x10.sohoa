using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Hubs;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace EvnHanoi.NotificationService.Controllers;

[ApiController]
[BypassDynamicPermission]
[SkipAudit]
[Route("api/v1/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IHubContext<NotificationHub> hubContext,
        INotificationRepository notificationRepository,
        ILogger<NotificationsController> logger)
    {
        _hubContext = hubContext;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    /// <summary>Danh sách thông báo của người dùng hiện tại.</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetForCurrentUser(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool onlyUnread = false)
    {
        var userId = JwtUserClaimResolver.ResolveUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        var (items, totalCount, unreadCount) = await _notificationRepository.GetForUserAsync(userId, page, pageSize, onlyUnread);
        return Ok(new { items, totalCount, unreadCount, page, pageSize });
    }

    /// <summary>Đánh dấu 1 thông báo đã đọc.</summary>
    [Authorize]
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var userId = JwtUserClaimResolver.ResolveUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        var updated = await _notificationRepository.MarkAsReadAsync(userId, id);
        if (!updated) return NotFound(new { message = "Không tìm thấy thông báo." });
        return Ok(new { success = true });
    }

    /// <summary>Đánh dấu tất cả thông báo của người dùng hiện tại là đã đọc.</summary>
    [Authorize]
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = JwtUserClaimResolver.ResolveUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        var affected = await _notificationRepository.MarkAllAsReadAsync(userId);
        return Ok(new { success = true, affected });
    }

    /// <summary>Xóa 1 thông báo của người dùng hiện tại.</summary>
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = JwtUserClaimResolver.ResolveUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        var deleted = await _notificationRepository.DeleteAsync(userId, id);
        if (!deleted) return NotFound(new { message = "Không tìm thấy thông báo." });
        return NoContent();
    }

    /// <summary>Xóa tất cả thông báo của người dùng hiện tại.</summary>
    [Authorize]
    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        var userId = JwtUserClaimResolver.ResolveUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        var affected = await _notificationRepository.DeleteAllAsync(userId);
        return Ok(new { success = true, affected });
    }

    /// <summary>EquipmentService gọi sau khi cập nhật progress từ RabbitMQ.</summary>
    [AllowAnonymous]
    [HttpPost("digitization-progress")]
    public async Task<IActionResult> PushDigitizationProgress([FromBody] DigitizationProgressPushDto message)
    {
        if (message.DossierId == Guid.Empty || message.DocumentVersionId == Guid.Empty)
            return BadRequest(new { message = "DossierId và DocumentVersionId là bắt buộc." });

        var group = NotificationHub.BuildDossierGroup(message.DossierId.ToString());
        await _hubContext.Clients.Group(group).SendAsync("ReceiveDigitizationProgress", message);
        _logger.LogInformation(
            "Pushed digitization progress to {Group}: version {VersionId}, phase {Phase}, progress {Progress}%",
            group,
            message.DocumentVersionId,
            message.Phase,
            message.Progress);
        return Ok(new { success = true, group });
    }

    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] JsonElement message)
    {
        // Broadcast the JSON message to all clients
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", message);
        return Ok(new { success = true, message = "Notification pushed successfully" });
    }
}
