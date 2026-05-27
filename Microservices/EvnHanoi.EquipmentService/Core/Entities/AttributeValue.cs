namespace EvnHanoi.EquipmentService.Core.Entities;

public class AttributeValue
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid AttributeDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
}
