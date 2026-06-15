using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class EquipmentType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int GridTypeId { get; set; }
    public int? SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatorId { get; set; }
    
    // Audit logs
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
