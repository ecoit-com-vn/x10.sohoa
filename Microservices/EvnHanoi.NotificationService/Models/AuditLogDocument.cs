namespace EvnHanoi.NotificationService.Models;

/// <summary>
/// Document ghi xuống Elasticsearch index audit_logs-*.
/// </summary>
public sealed class AuditLogDocument
{
    public string Id { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? ActorIp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? Details { get; set; }
    public string? HttpMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
    public string? CorrelationId { get; set; }
    public bool IsDeleted { get; set; }
    public string? LogGroup { get; set; }
    public string? ActorUnitId { get; set; }
    public string? ActorUnitName { get; set; }
    public string? ActorFullName { get; set; }
}
