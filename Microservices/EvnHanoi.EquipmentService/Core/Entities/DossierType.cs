using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class DossierType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? FormId { get; set; }
    public bool IsActive { get; set; } = true;
    public int? Piority { get; set; }
    
    // Join field (populated by repository)
    public string? FormName { get; set; }

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}
