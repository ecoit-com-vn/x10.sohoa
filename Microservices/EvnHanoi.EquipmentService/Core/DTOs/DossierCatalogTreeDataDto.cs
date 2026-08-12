namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// Dữ liệu tổng hợp cho cây danh mục hồ sơ — lấy trong 1 round-trip duy nhất.
/// </summary>
public class DossierCatalogTreeDataDto
{
    public UnitQueryDto? UnitInfo { get; set; }
    public List<InfrastructureQueryDto> Infrastructures { get; set; } = new();
    public List<ActiveDossierQueryDto> Dossiers { get; set; } = new();
    public List<DossierInfrastructureLinkDto> JunctionLinks { get; set; } = new();
    public Dictionary<string, int> DocumentCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Liên kết n-n Dossier <-> Infrastructure.
/// </summary>
public class DossierInfrastructureLinkDto
{
    public string DossierId { get; set; } = string.Empty;
    public string InfrastructureId { get; set; } = string.Empty;
}