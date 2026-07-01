namespace EvnHanoi.EquipmentService.Core.Entities;

public class Equipment
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Mã thiết bị
    public string SerialNumber { get; set; } = string.Empty;
    
    public Guid? InfrastructureId { get; set; }
    public Guid? CountryId { get; set; }
    public bool IsActive { get; set; } = true; 
    public Guid? CreatorId { get; set; }
    
    // Audit fields
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
    public string? FormValues { get; set; }
    public long? UnitId { get; set; }
}
