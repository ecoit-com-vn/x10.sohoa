namespace EvnHanoi.NotificationService.Models;

public class DossierEsDocument
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
    public string Status { get; set; } = string.Empty;
    public string? WorkflowStatusName { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public string? CreatorId { get; set; }
    public string? CreatorUsername { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int DocumentCount { get; set; }
    public int CurrentVersionNumber { get; set; }
    public bool IsDeleted { get; set; }
    public List<DossierCatalogFieldEs> CatalogFields { get; set; } = new();
    public List<DossierFormFieldEs> FormFields { get; set; } = new();
    public List<DossierEquipmentEs> Equipments { get; set; } = new();
}

public class DossierCatalogFieldEs
{
    public string CatalogCode { get; set; } = string.Empty;
    public string CatalogName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class DossierFormFieldEs
{
    public string FieldCode { get; set; } = string.Empty;
    public string? TextValue { get; set; }
    public double? NumericValue { get; set; }
    public DateTime? DateValue { get; set; }
}

public class DossierEquipmentEs
{
    public string EquipmentId { get; set; } = string.Empty;
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public string? SerialNumber { get; set; }
}
