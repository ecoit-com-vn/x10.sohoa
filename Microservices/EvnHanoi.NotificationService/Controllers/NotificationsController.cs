using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Hubs;
using EvnHanoi.NotificationService.Models;
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
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationsController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
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
