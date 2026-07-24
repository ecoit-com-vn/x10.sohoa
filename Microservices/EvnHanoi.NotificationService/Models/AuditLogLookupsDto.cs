namespace EvnHanoi.NotificationService.Models;

public sealed class AuditLogLookupItem
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class AuditLogLookupsDto
{
    public List<AuditLogLookupItem> Actions { get; set; } = new();
    public List<AuditLogLookupItem> ResourceTypes { get; set; } = new();
    public List<AuditLogLookupItem> LogGroups { get; set; } = new();
}
