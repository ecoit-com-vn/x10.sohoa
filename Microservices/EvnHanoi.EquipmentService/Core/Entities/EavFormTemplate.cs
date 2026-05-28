namespace EvnHanoi.EquipmentService.Core.Entities;

public class EavFormTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // JSON schema or fields definition
    public string Schema { get; set; } = string.Empty;

    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
