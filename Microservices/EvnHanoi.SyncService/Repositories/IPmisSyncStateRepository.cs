using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Repositories;

public class PmisSyncStateRow
{
    public string PmisCode { get; set; } = string.Empty;
    public string? ContentHash { get; set; }
    public int HashVersion { get; set; }
    public bool DetailSynced { get; set; }
    public DateTime? LastPushedAt { get; set; }
    public DateTime? LastScanAt { get; set; }
}

/// <summary>1 bản ghi PMIS vừa đẩy thành công — hash mới + đã lấy đủ chi tiết (thiết bị) hay chưa.</summary>
public class PmisSyncStateUpsert
{
    public string PmisCode { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public bool DetailSynced { get; set; }
}

/// <summary>Bảng PMIS_SYNC_STATE (Migration0014) — trạng thái đồng bộ tăng dần. Mọi mốc thời gian ghi bằng
/// <see cref="DateTime.UtcNow"/> (không dùng SYSTIMESTAMP) để cùng hệ quy chiếu với code so sánh, tránh lệch
/// múi giờ như đã gặp ở SYNC_HISTORY.</summary>
public interface IPmisSyncStateRepository
{
    /// <summary>Đọc trạng thái của các mã (chia lô 900 vì giới hạn IN của Oracle). Mã không có trong kết quả =
    /// chưa từng đẩy. Từ điển không phân biệt hoa/thường.</summary>
    Task<Dictionary<string, PmisSyncStateRow>> GetAsync(string objectType, IReadOnlyCollection<string> codes);

    /// <summary>Đọc TOÀN BỘ dòng của 1 loại (INFRA/PARENT_SCAN ~40k dòng) — dùng khi chọn cha để quét.</summary>
    Task<Dictionary<string, PmisSyncStateRow>> GetAllAsync(string objectType);

    /// <summary>Ghi hash + HASH_VERSION + DETAIL_SYNCED, đặt LAST_PUSHED_AT/LAST_SEEN_AT = bây giờ (MERGE theo lô).</summary>
    Task UpsertPushedAsync(string objectType, IReadOnlyCollection<PmisSyncStateUpsert> rows, int hashVersion);

    /// <summary>Chỉ cập nhật LAST_SEEN_AT cho các mã không đổi (không đụng hash/LAST_PUSHED_AT).</summary>
    Task TouchSeenAsync(string objectType, IReadOnlyCollection<string> codes);

    /// <summary>Đánh dấu vừa quét xong thiết bị con của 1 cha (OBJECT_TYPE='PARENT_SCAN').</summary>
    Task MarkParentScannedAsync(string parentPmisCode);

    /// <summary>Lần quét đầy đủ gần nhất của 1 loại đối tượng (dòng SWEEP); null nếu chưa từng.</summary>
    Task<DateTime?> GetSweepAtAsync(string objectType);

    Task SetSweepAtAsync(string objectType);

    /// <summary>Trạng thái đồng bộ tài liệu theo owner ('DOC_OWNER') hoặc khoảng ngày ('DOC_WINDOW'); mã không có = chưa từng.</summary>
    Task<Dictionary<string, DocumentOwnerState>> GetDocumentStatesAsync(string objectType);

    /// <summary>Như trên nhưng chỉ các mã bắt đầu bằng <paramref name="codePrefix"/> (vd "owner|" cho các khoảng ngày của 1 owner).</summary>
    Task<Dictionary<string, DocumentOwnerState>> GetDocumentStatesByPrefixAsync(string objectType, string codePrefix);

    /// <summary>Ghi (MERGE) toàn bộ trạng thái tài liệu của 1 dòng.</summary>
    Task UpsertDocumentStateAsync(string objectType, DocumentOwnerState state);
}
