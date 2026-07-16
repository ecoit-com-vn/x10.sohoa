using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class EavFormTemplateVersion
{
    public Guid Id { get; set; }
    public Guid FormTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DescriptionInfo { get; set; } = string.Empty;
    public string? ExtractionPosition { get; set; }
    public string FormSchema { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Status { get; set; } = "Tạo mới";
    public bool IsDeleted { get; set; } = false;
}
