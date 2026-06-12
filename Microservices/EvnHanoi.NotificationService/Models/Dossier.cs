using System;

namespace EvnHanoi.NotificationService.Models;

public class Dossier
{
    public string Id { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PublishStatus { get; set; } = string.Empty;
    public long? UnitId { get; set; }
}
