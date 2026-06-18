namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Bộ/Gói hồ sơ lớn - container chứa nhiều hồ sơ
/// </summary>
public class DossierSet
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? UnitId { get; set; }

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}
