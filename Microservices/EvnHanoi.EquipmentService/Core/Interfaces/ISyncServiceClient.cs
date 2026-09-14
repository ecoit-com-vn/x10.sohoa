namespace EvnHanoi.EquipmentService.Core.Interfaces;

/// <summary>Gọi API nội bộ (internal/v1/...) của SyncService. Hiện chỉ có 1 việc: yêu cầu chạy sớm 1
/// lượt đồng bộ PMIS đầy đủ — xem PmisUnitCodeMappingController.</summary>
public interface ISyncServiceClient
{
    /// <summary>Yêu cầu SyncService chạy sớm 1 lượt đồng bộ đầy đủ (Trạm/Đường dây/Thiết bị) ngay khi có
    /// thể (trong vòng &lt;=1 phút, xem PmisScheduledSyncJob), thay vì chờ đúng lịch cấu hình — dùng khi
    /// vừa thêm 1 ánh xạ đơn vị PMIS mới để tự tính lại UnitId cho các bản ghi trước đó chưa xác định
    /// được đơn vị. Lỗi (SyncService tạm gián đoạn) chỉ log cảnh báo, KHÔNG chặn việc thêm ánh xạ — lượt
    /// đồng bộ theo lịch thường vẫn sẽ tự sửa đúng sau đó.</summary>
    Task TriggerSyncNowAsync();
}
