using System.Collections.Generic;

namespace EvnHanoi.NotificationService.Models;

public sealed class DeleteAuditLogsByIdsRequest
{
    public List<string> Ids { get; set; } = new();
}
