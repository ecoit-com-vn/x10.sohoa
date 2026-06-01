namespace EvnHanoi.EquipmentService.Core.Entities;

public class Equipment
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Mã thiết bị
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public long? UnitId { get; set; }
}
