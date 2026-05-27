using EvnHanoi.NotificationService.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace EvnHanoi.NotificationService.Services;

public class NotificationDispatcher
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(IHubContext<NotificationHub> hubContext, ILogger<NotificationDispatcher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationAsync(string message)
    {
        _logger.LogInformation("Sending notification: {Message}", message);
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", message);
    }

    public async Task SendNotificationToUserAsync(string userId, string message)
    {
        _logger.LogInformation("Sending notification to user {UserId}: {Message}", userId, message);
        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", message);
    }
}
