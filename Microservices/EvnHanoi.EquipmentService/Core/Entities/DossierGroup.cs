namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Nhóm hồ sơ — khớp bảng DOSSIER_GROUPS.
/// INFRA_TYPE_ID: 1 = Trạm, 2 = Đường dây (INFRASTRUCTURE_TYPE).
/// </summary>
public class DossierGroup
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int InfraTypeId { get; set; }
    public bool IsEquipmentDossier { get; set; }
}

/// <summary>
/// Hằng số / helper validate nhanh — seed khớp migration DOSSIER_GROUPS.
/// </summary>
public static class DossierGroupConstants
{
    public const int Station = 1;
    public const int TransmissionLine = 2;
    public const int StationEquipment = 3;
    public const int LineEquipment = 4;

    public static bool IsKnownId(int id) =>
        id is Station or TransmissionLine or StationEquipment or LineEquipment;

    public static bool IsEquipmentDossierId(int id) =>
        id is StationEquipment or LineEquipment;

    public static int ResolveInfraTypeId(int dossierGroupId) => dossierGroupId switch
    {
        Station or StationEquipment => 1,
        TransmissionLine or LineEquipment => 2,
        _ => throw new KeyNotFoundException($"DossierGroup không hợp lệ: {dossierGroupId}.")
    };
}
