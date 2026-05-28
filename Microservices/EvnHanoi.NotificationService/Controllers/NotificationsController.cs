using EvnHanoi.NotificationService.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace EvnHanoi.NotificationService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationsController(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] JsonElement message)
    {
        // Broadcast the JSON message to all clients
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", message);
        return Ok(new { success = true, message = "Notification pushed successfully" });
    }
}
