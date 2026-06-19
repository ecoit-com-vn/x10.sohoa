namespace EvnHanoi.NotificationService.Models;

public class DossierListItemDto
{
    public Guid Id { get; set; }
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public Guid? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public Guid? DossierSetId { get; set; }
    public string? DossierSetName { get; set; }
    public Guid DossierTypeId { get; set; }
    public string? DossierTypeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? WorkflowStatusName { get; set; }
    public int DocumentCount { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; }
    public Dictionary<string, string> CatalogData { get; set; } = new();
}

public class DossierFilterDto
{
    public string? Keyword { get; set; }
    public Guid? InfrastructureId { get; set; }
    public int? GridTypeId { get; set; }
    public long? UnitId { get; set; }
    public IReadOnlyList<long>? UnitScopeIds { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
