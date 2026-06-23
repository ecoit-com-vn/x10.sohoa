using System;

namespace EvnHanoi.NotificationService.Models;

public class EavFormTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = "Tạo mới";
    public string FormType { get; set; } = "FORM";
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public string? ExtractionProcess { get; set; }
}
