namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEquipmentPmisSpecRepository
{
    /// <summary>1 dòng/thiết bị — ghi đè mỗi lần đồng bộ vì đây là "bản sao mới nhất từ PMIS", KHÔNG đụng EQUIPMENTS.FormValues.</summary>
    Task UpsertAsync(Guid equipmentId, string? formValuesJson, string? syncHistoryId);

    /// <summary>Dùng cho tính năng so sánh sai khác trên màn chi tiết thiết bị (module 6).</summary>
    Task<(string? FormValues, DateTime? SyncedAt)?> GetByEquipmentIdAsync(Guid equipmentId);
}
