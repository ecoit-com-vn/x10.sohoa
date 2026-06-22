namespace EvnHanoi.EquipmentService.Infrastructure.Models;

/// <summary>Read model map từ index dossier_index (NotificationService).</summary>
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
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; }
    public int DocumentCount { get; set; }
    public bool IsDeleted { get; set; }
    public List<DossierCatalogFieldEs> CatalogFields { get; set; } = new();
    public List<DossierFormFieldEs> FormFields { get; set; } = new();
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
}
