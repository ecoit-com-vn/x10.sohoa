namespace EvnHanoi.ReportService.Core.Models;

public enum ReportDossierKind
{
    GridType,
    Equipment,
    Station,
    Line
}

public class ReportDossierLookupItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public class ReportDossierBhsColumn
{
    public string Key { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class ReportDossierSearchRequest
{
    public long? UnitId { get; set; }
    public int? GridTypeId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public int? InfrastructureTypeId { get; set; }
    public Guid? EquipmentId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public bool IsAdmin { get; set; }
    public long? UserUnitId { get; set; }
}

public class ReportDossierSearchResponse
{
    public List<ReportDossierListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UserScope
{
    public bool IsAdmin { get; init; }
    public long? UnitId { get; init; }

    public long? EffectiveFilterUnitId(long? selectedUnitId) =>
        IsAdmin ? selectedUnitId : UnitId;
}
