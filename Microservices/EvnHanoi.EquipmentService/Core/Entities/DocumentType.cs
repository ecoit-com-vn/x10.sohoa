using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class DocumentType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? FormId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEquipmentProfile { get; set; }
    public bool IsFactoryAcceptanceReport { get; set; }
    public int? Piority { get; set; }

    public string? FormName { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}
