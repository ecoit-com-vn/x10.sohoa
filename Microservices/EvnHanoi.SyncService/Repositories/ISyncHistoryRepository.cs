using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface ISyncHistoryRepository
{
    Task<string> CreateAsync(SyncHistory history);
    Task CompleteAsync(string id, string status, int totalRecords, int successRecords, int failedRecords, string? errorMessage);
    Task InsertDetailsAsync(IEnumerable<SyncHistoryDetail> details);
    Task<(IEnumerable<SyncHistory> Items, int TotalCount)> GetPagedAsync(string? objectType, int page, int pageSize);
    Task<(IEnumerable<SyncHistoryDetail> Items, int TotalCount)> GetDetailsPagedAsync(string syncHistoryId, int page, int pageSize);

    /// <summary>Xoá thủ công theo yêu cầu admin (nút "Xoá lịch sử") — SYNC_HISTORY_DETAIL tự động xoá
    /// theo (ON DELETE CASCADE). <paramref name="mode"/>: "DATE_RANGE" (dùng fromDate/toDate),
    /// "KEEP_LAST_1_DAY"/"KEEP_LAST_7_DAYS" (giữ N ngày gần nhất, Oracle tự tính bằng SYSTIMESTAMP —
    /// không tính cutoff bên C# để tránh lệch múi giờ), "ALL" (xoá hết). Trả về số dòng SYNC_HISTORY đã xoá.</summary>
    Task<int> DeleteAsync(string objectType, string mode, DateTime? fromDate, DateTime? toDate);

    /// <summary>Dọn tự động (Quartz, xem SyncHistoryCleanupJob) — xoá SYNC_HISTORY (mọi đối tượng) cũ hơn
    /// <paramref name="retentionDays"/> ngày, Oracle tự tính cutoff bằng SYSTIMESTAMP.</summary>
    Task<int> DeleteOlderThanAsync(int retentionDays);
}
