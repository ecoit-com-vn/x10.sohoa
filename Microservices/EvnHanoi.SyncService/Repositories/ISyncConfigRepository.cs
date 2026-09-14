using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface ISyncConfigRepository
{
    Task<IEnumerable<SyncConfig>> GetAllAsync();
    Task<SyncConfig?> GetByObjectTypeAsync(string objectType);
    Task<bool> UpdateAsync(string objectType, UpdateSyncConfigRequest request, string? modifiedBy);
    Task UpdateRunResultAsync(string objectType, DateTime lastSyncAt, DateTime? nextSyncAt, int consecutiveFailureCount);

    /// <summary>Đặt NEXT_SYNC_AT = ngay bây giờ cho các đối tượng đang BẬT — để PmisScheduledSyncJob (tick
    /// mỗi phút) tự nhận là "đã tới hạn" và chạy 1 lượt đồng bộ đầy đủ (chọn tất cả) sớm hơn lịch thường,
    /// KHÔNG tạo job/migration riêng. Dùng khi vừa thêm 1 ánh xạ đơn vị PMIS mới (xem
    /// PmisUnitCodeMappingController.EquipmentService) — lượt đồng bộ đầy đủ kế tiếp sẽ tự tính lại
    /// UnitId cho MỌI bản ghi (kể cả những bản ghi trước đó không xác định được đơn vị) vì
    /// InfrastructureRepository/EquipmentRepository.UpsertFromPmisAsync luôn tra lại PMIS_UNIT_CODE_MAPPING
    /// theo mã đơn vị PMIS mới nhất mỗi lần chạy, không có gì cần "vá" thêm ở đây ngoài việc chạy sớm hơn.
    /// Đối tượng đang TẮT giữ nguyên, không tự bật lại.</summary>
    Task MarkDueNowAsync(IEnumerable<string> objectTypes);
}
