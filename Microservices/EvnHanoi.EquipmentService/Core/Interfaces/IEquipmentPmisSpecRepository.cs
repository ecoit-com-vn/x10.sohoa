namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEquipmentPmisSpecRepository
{
    /// <summary>1 dòng/thiết bị — ghi đè mỗi lần đồng bộ vì đây là "bản sao mới nhất từ PMIS", KHÔNG đụng EQUIPMENTS.FormValues.</summary>
    Task UpsertAsync(Guid equipmentId, string? formValuesJson, string? syncHistoryId);

    /// <summary>Dùng cho tính năng so sánh sai khác trên màn chi tiết thiết bị (module 6).</summary>
    Task<(string? FormValues, DateTime? SyncedAt)?> GetByEquipmentIdAsync(Guid equipmentId);

    /// <summary>
    /// Thông số PMIS của các thiết bị đồng bộ gần nhất thuộc 1 loại thiết bị — dùng để rút ra danh sách
    /// khoá PMIS thật gợi ý cho admin khi khai "Tên trường PMIS" trong Form Builder. Giới hạn số dòng
    /// để không quét cả bảng: các thiết bị cùng loại có cùng bộ khoá nên vài chục dòng là đủ phủ.
    /// </summary>
    Task<IEnumerable<string?>> GetRecentFormValuesByEquipmentTypeAsync(Guid equipmentTypeId, int maxRows);
}
