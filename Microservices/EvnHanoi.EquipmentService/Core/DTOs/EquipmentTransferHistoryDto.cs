using System;

namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// 1 dòng lịch sử di chuyển thiết bị — Trạm/Đường dây chuyển ĐI và Trạm/Đường dây nhận (ĐẾN), cùng
/// thời điểm chuyển. Popup "Lịch sử di chuyển thiết bị" hiển thị dữ liệu này.
/// </summary>
public class EquipmentTransferHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public Guid? SourceInfrastructureId { get; set; }
    public string? SourceInfrastructureName { get; set; }
    public string? SourceInfrastructureCode { get; set; }
    public Guid TargetInfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public long? TargetUnitId { get; set; }
    public string? UnitName { get; set; }
    public string? Note { get; set; }
    public string? TransferredBy { get; set; }
    public DateTime TransferredAt { get; set; }
}
