using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class Catalog
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// FK tới CATALOG_TYPE.Id (thay thế chuỗi CatalogType cũ)
    /// </summary>
    public long CatalogTypeId { get; set; }

    public long? ParentId { get; set; }
    public string? Description { get; set; }
    public long? UnitId { get; set; }
    public string? UnitName { get; set; }
    public int Priority { get; set; } = 1;
    public int Status { get; set; } = 1; // 1 = Active, 0 = Locked
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
}
