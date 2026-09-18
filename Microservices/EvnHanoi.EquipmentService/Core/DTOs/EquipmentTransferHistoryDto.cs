using System;

namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// 1 dòng lịch sử di chuyển thiết bị — Trạm/Đường dây + Đơn vị quản lý thiết bị được chuyển ĐẾN, và
/// thời điểm chuyển. Popup "Lịch sử di chuyển thiết bị" chỉ hiển thị dữ liệu này, không hiển thị trạm/
/// đơn vị cũ.
/// </summary>
public class EquipmentTransferHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public Guid TargetInfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public long? TargetUnitId { get; set; }
    public string? UnitName { get; set; }
    public string? Note { get; set; }
    public string? TransferredBy { get; set; }
    public DateTime TransferredAt { get; set; }
}
