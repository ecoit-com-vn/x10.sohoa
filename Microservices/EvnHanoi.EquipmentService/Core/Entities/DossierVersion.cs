namespace EvnHanoi.EquipmentService.Core.Entities;

public class DossierVersion
{
    public Guid Id { get; set; }
    public Guid DossierId { get; set; }
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ChangeLog { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
