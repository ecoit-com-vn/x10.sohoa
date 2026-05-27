namespace EvnHanoi.EquipmentService.Core.Entities;

public class Dossier
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // e.g. Draft, Submitted, Approved, Rejected
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    
    // Optimistic Locking
    public int Version { get; set; }
}
