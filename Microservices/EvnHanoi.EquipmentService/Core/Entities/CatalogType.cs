using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class CatalogType
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int HasParent { get; set; }
    public string? Description { get; set; }
    public bool IsPrivate { get; set; }
    public int Status { get; set; } = 1; // 1 = Active, 0 = Inactive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
