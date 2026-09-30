using EvnHanoi.SyncService.Repositories;

namespace EvnHanoi.SyncService.Services;

/// <summary>Ngữ cảnh đồng bộ tăng dần cho 1 trang bản ghi PMIS. Truyền null vào SyncInfrastructureAsync/
/// SyncEquipmentAsync = hành vi cũ (đẩy tất cả). Job tạo đối tượng này (điền <see cref="Existing"/>), executor
/// điền <see cref="ToSave"/>/<see cref="UnchangedCodes"/>, job đọc lại để ghi PMIS_SYNC_STATE.</summary>
public class IncrementalContext
{
    /// <summary>Trạng thái đã có của các mã trong trang này (từ điển không phân biệt hoa/thường).</summary>
    public required IReadOnlyDictionary<string, PmisSyncStateRow> Existing { get; init; }

    /// <summary>Có giá trị = đang trong đợt quét đầy đủ bắt đầu lúc này (UTC): bản ghi phải được đẩy lại nếu chưa
    /// được đẩy kể từ mốc đó, dù hash không đổi. Dùng mốc thời gian (thay vì cờ) để đợt quét đầy đủ TIẾP TỤC được
    /// qua nhiều lượt nếu bị cắt giữa chừng — bản ghi đã đẩy sau mốc thì được bỏ qua. null = chế độ thường.</summary>
    public DateTime? SweepStartUtc { get; init; }

    public int HashVersion { get; init; } = 1;

    // ---- OUTPUT do PmisSyncExecutionService điền ----
    /// <summary>Mã đã đẩy thành công + hash mới + đã đủ chi tiết → job gọi UpsertPushedAsync.</summary>
    public List<PmisSyncStateUpsert> ToSave { get; } = new();

    /// <summary>Mã không đổi bị bỏ qua → job gọi TouchSeenAsync và cộng vào Success.</summary>
    public List<string> UnchangedCodes { get; } = new();

    /// <summary>true nếu bản ghi giữ nguyên (cùng hash, cùng phiên bản băm, và — nếu cần — đã lấy đủ chi tiết)
    /// so với lần đẩy thành công gần nhất, và (khi đang quét đầy đủ) đã được đẩy sau mốc bắt đầu đợt quét.</summary>
    public bool IsUnchanged(string code, string hash, bool requireDetail) =>
        Existing.TryGetValue(code, out var row)
        && row.ContentHash == hash
        && row.HashVersion == HashVersion
        && (SweepStartUtc == null || (row.LastPushedAt != null && row.LastPushedAt >= SweepStartUtc))
        && (!requireDetail || row.DetailSynced);
}
