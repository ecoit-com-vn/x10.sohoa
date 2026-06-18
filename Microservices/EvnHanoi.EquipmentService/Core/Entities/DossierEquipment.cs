namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Bảng liên kết nhiều-nhiều giữa Dossier và Equipment
/// </summary>
public class DossierEquipment
{
    public Guid DossierId { get; set; }
    public Guid EquipmentId { get; set; }
}
