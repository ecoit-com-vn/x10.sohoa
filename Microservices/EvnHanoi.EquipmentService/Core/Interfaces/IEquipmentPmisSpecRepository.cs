namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEquipmentPmisSpecRepository
{
    /// <summary>1 dòng/thiết bị — ghi đè mỗi lần đồng bộ vì đây là "bản sao mới nhất từ PMIS", KHÔNG đụng
    /// EQUIPMENTS.FormValues. <paramref name="fieldLabelsJson"/> là nhãn tiếng Việt cho từng khoá của
    /// <paramref name="formValuesJson"/> (field PMIS "tenThongSoKyThuat", bổ sung 2026-09-23) — có thể
    /// null nếu PMIS/nguồn gọi chưa cung cấp (vd. đồng bộ cũ trước khi có field này).</summary>
    Task UpsertAsync(Guid equipmentId, string? formValuesJson, string? syncHistoryId, string? fieldLabelsJson = null);

    /// <summary>Dùng cho tính năng so sánh sai khác trên màn chi tiết thiết bị (module 6).</summary>
    Task<(string? FormValues, string? FieldLabels, DateTime? SyncedAt)?> GetByEquipmentIdAsync(Guid equipmentId);

    /// <summary>
    /// Thông số PMIS (kèm nhãn tiếng Việt nếu có) của các thiết bị đồng bộ gần nhất thuộc 1 loại thiết
    /// bị — dùng để rút ra danh sách khoá PMIS thật gợi ý cho admin khi khai "Tên trường PMIS" trong
    /// Form Builder. Giới hạn số dòng để không quét cả bảng: các thiết bị cùng loại có cùng bộ khoá nên
    /// vài chục dòng là đủ phủ.
    /// </summary>
    Task<IEnumerable<(string? FormValues, string? FieldLabels)>> GetRecentFormValuesByEquipmentTypeAsync(Guid equipmentTypeId, int maxRows);
}
