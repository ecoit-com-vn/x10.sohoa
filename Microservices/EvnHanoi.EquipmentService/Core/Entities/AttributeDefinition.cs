namespace EvnHanoi.EquipmentService.Core.Entities;

public class AttributeDefinition
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty; // e.g. String, Number, Date, Boolean
    public bool IsRequired { get; set; }
}
