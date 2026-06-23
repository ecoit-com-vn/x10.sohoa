using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace EvnHanoi.NotificationService.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinDossier(string dossierId)
    {
        if (string.IsNullOrWhiteSpace(dossierId)) return;
        var group = BuildDossierGroup(dossierId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogInformation("Client {ConnectionId} joined group {Group}", Context.ConnectionId, group);
    }

    public async Task LeaveDossier(string dossierId)
    {
        if (string.IsNullOrWhiteSpace(dossierId)) return;
        var group = BuildDossierGroup(dossierId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("Client {ConnectionId} left group {Group}", Context.ConnectionId, group);
    }

    internal static string BuildDossierGroup(string dossierId) =>
        $"dossier-{dossierId.Trim().ToLowerInvariant()}";

    public async Task SendNotification(string message)
    {
        await Clients.All.SendAsync("ReceiveNotification", message);
    }
}
