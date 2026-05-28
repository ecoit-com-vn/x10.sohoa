using System;
using System.Collections.Generic;

namespace EvnHanoi.SyncService.Models;

public class EquipmentSyncMessage
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
}
