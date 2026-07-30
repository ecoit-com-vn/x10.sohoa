namespace EvnHanoi.Infrastructure.Messaging;

/// <summary>Hồ sơ đã chuyển sang bước xử lý mới — người nhận là các assignee đang Pending của bước mới.</summary>
public class DossierMovedEvent
{
    public string DossierId { get; set; } = string.Empty;
    public Guid InstanceId { get; set; }
    public string? StepName { get; set; }
    public List<string> RecipientUserIds { get; set; } = new();
    public string? ActorUserId { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>Thiết bị được chuyển sang TBA (Infrastructure) mới — thông báo cho tài khoản cả đơn vị cũ và mới.</summary>
public class EquipmentTbaTransferredEvent
{
    public Guid EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    public long? OldUnitId { get; set; }
    public long? NewUnitId { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>Hồ sơ/tài liệu của thiết bị đã được chuyển sang bản ghi ở TBA mới — chỉ thông báo cho đơn vị nhận.</summary>
public class EquipmentDossierTransferredEvent
{
    public Guid EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    public long? OldUnitId { get; set; }
    public long? NewUnitId { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime Timestamp { get; set; }
}
