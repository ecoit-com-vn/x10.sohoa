namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Lưu snapshot FormDataJson tại mỗi lần lưu - phục vụ lịch sử phiên bản
/// </summary>
public class DossierVersion
{
    public Guid Id { get; set; }
    public Guid DossierId { get; set; }
    public int VersionNumber { get; set; }
    public string? FormDataJson { get; set; }
    public string? ChangeNote { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
