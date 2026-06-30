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
    public int StatusId { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusName { get; set; }
    public string? WorkflowStatusName { get; set; }
    public string? CreatorId { get; set; }
    public string? CreatorUsername { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; }
    public int DocumentCount { get; set; }
    public bool IsDeleted { get; set; }
    public string? CurrentStepId { get; set; }
    public List<string> CurrentAssignees { get; set; } = new();
    public List<WorkflowActionEsDto> AvailableActions { get; set; } = new();
    public List<DossierCatalogFieldEs> CatalogFields { get; set; } = new();
    public List<DossierFormFieldEs> FormFields { get; set; } = new();

    public int? PublishStatusId { get; set; }
    public string? PublishStatusCode { get; set; }
    public string? PublishStatusName { get; set; }
}

public class WorkflowActionEsDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NextNodeId { get; set; } = string.Empty;
    public bool RequiresNextAssignee { get; set; }
    public string? NextStepRole { get; set; }
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
