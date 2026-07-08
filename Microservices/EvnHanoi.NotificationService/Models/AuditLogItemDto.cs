namespace EvnHanoi.NotificationService.Models;

public sealed class AuditLogItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? ServiceName { get; set; }
    public int? StatusCode { get; set; }
    public string? HttpMethod { get; set; }
    public string? RequestPath { get; set; }
}
