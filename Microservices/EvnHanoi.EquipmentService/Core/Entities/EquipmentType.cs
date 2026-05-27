namespace EvnHanoi.EquipmentService.Core.Entities;

public class EquipmentType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
