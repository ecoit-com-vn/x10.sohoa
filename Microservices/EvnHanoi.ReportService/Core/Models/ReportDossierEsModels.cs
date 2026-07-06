namespace EvnHanoi.ReportService.Core.Models;

public class BhsCatalogDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class ReportDossierEsDocument
{
    public string Id { get; set; } = string.Empty;
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public string? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public long? UnitId { get; set; }
    public string? UnitName { get; set; }
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
    public int? PublishStatusId { get; set; }
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
}

public class DossierEquipmentEs
{
    public string EquipmentId { get; set; } = string.Empty;
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
}

public static class ReportDossierEsFieldNames
{
    public const string Id = "id";
    public const string IsDeleted = "isDeleted";
    public const string GridTypeId = "gridTypeId";
    public const string InfrastructureId = "infrastructureId";
    public const string UnitId = "unitId";
    public const string PublishStatusId = "publishStatusId";
    public const string EquipmentId = "equipments.equipmentId";
    public const string CreatedDate = "createdDate";
}

public class ReportDossierListItem
{
    public Guid Id { get; set; }
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public Guid? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public long? UnitId { get; set; }
    public string? UnitName { get; set; }
    public string? EquipmentName { get; set; }
    public Guid? DossierSetId { get; set; }
    public string? DossierSetName { get; set; }
    public Guid DossierTypeId { get; set; }
    public string? DossierTypeName { get; set; }
    public int StatusId { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusName { get; set; }
    public int DocumentCount { get; set; }
    public ReportCreatorInfo? Creator { get; set; }
    public DateTime CreatedDate { get; set; }
    public Dictionary<string, string> CatalogData { get; set; } = new();
}

public class ReportCreatorInfo
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ReportDossierDetailDto
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
    public Guid? FormId { get; set; }
    public string? FormDataJson { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusCode { get; set; }
    public int KindId { get; set; } = 2;
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatusName { get; set; }
    public int RowVersion { get; set; }
    public ReportCreatorInfo? Creator { get; set; }
    public List<ReportDossierEquipmentDto> Equipments { get; set; } = new();
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int? PublishStatusId { get; set; }
    public string? PublishStatusCode { get; set; }
    public string? PublishStatusName { get; set; }
}

public class ReportDossierEquipmentDto
{
    public Guid EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public string? SerialNumber { get; set; }
    public string? EquipmentTypeName { get; set; }
    public string? InfrastructureName { get; set; }
}

public class ReportDocumentListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? FolderId { get; set; }
    public Guid? DossierId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public Guid? LatestVersionId { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
}

public class ReportDocumentFilterDto
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ReportDownloadTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public string? Url { get; set; }
    public string? DownloadUrl { get; set; }
}
