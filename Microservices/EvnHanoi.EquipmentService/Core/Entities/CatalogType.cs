using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class CatalogType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int HasParent { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
