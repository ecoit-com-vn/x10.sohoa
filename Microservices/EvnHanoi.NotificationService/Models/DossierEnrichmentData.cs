namespace EvnHanoi.NotificationService.Models;

public class DossierEnrichmentData
{
    public string Id { get; set; } = string.Empty;
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public string? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public long? UnitId { get; set; }
    public string? DossierSetId { get; set; }
    public string? DossierSetName { get; set; }
    public string DossierTypeId { get; set; } = string.Empty;
    public string? DossierTypeName { get; set; }
    public string? FormDataJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? WorkflowStatusName { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public string? CreatorId { get; set; }
    public string? CreatorUsername { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int CurrentVersionNumber { get; set; }
    public bool IsDeleted { get; set; }
}

public class BhsCatalogDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class DossierEquipmentEnrichment
{
    public string EquipmentId { get; set; } = string.Empty;
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public string? SerialNumber { get; set; }
}
