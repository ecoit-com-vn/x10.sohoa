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
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildDossierGroup(dossierId));
        _logger.LogDebug("Client {ConnectionId} joined dossier group {DossierId}", Context.ConnectionId, dossierId);
    }

    public async Task LeaveDossier(string dossierId)
    {
        if (string.IsNullOrWhiteSpace(dossierId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildDossierGroup(dossierId));
        _logger.LogDebug("Client {ConnectionId} left dossier group {DossierId}", Context.ConnectionId, dossierId);
    }

    internal static string BuildDossierGroup(string dossierId) => $"dossier-{dossierId.Trim()}";

    public async Task SendNotification(string message)
    {
        await Clients.All.SendAsync("ReceiveNotification", message);
    }
}
