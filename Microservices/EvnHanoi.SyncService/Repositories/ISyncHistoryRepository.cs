using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface ISyncHistoryRepository
{
    Task<string> CreateAsync(SyncHistory history);

    /// <summary>Chỉ cập nhật khi dòng vẫn đang STATUS = RUNNING (guard chống ghi đè lượt đã bị
    /// SyncHistoryWatchdogJob đánh FAILED trước đó — xem FailStaleRunningAsync). Trả về false nếu không
    /// khớp dòng nào (đã bị watchdog/lời gọi khác finalize trước) — caller nên log cảnh báo khi false,
    /// vì kết quả thật của lượt chạy này (total/success/failed/errorMessage) đã bị bỏ qua.</summary>
    Task<bool> CompleteAsync(string id, string status, int totalRecords, int successRecords, int failedRecords, string? errorMessage);
    Task InsertDetailsAsync(IEnumerable<SyncHistoryDetail> details);
    Task<(IEnumerable<SyncHistory> Items, int TotalCount)> GetPagedAsync(string? objectType, int page, int pageSize);
    /// <summary>recordKind: lọc theo INFRASTRUCTURE/EQUIPMENT/DOCUMENT (xem SyncRecordKind) — null = không lọc
    /// (hành vi cũ). Dữ liệu lịch sử chưa phân loại (RECORD_KIND NULL) tính vào INFRASTRUCTURE/EQUIPMENT,
    /// không tính vào DOCUMENT.</summary>
    Task<(IEnumerable<SyncHistoryDetail> Items, int TotalCount)> GetDetailsPagedAsync(string syncHistoryId, int page, int pageSize, string? recordKind = null);

    /// <summary>Xoá thủ công theo yêu cầu admin (nút "Xoá lịch sử") — SYNC_HISTORY_DETAIL tự động xoá
    /// theo (ON DELETE CASCADE). <paramref name="mode"/>: "DATE_RANGE" (dùng fromDate/toDate),
    /// "KEEP_LAST_1_DAY"/"KEEP_LAST_7_DAYS" (giữ N ngày gần nhất, Oracle tự tính bằng SYSTIMESTAMP —
    /// không tính cutoff bên C# để tránh lệch múi giờ), "ALL" (xoá hết). Trả về số dòng SYNC_HISTORY đã xoá.</summary>
    Task<int> DeleteAsync(string objectType, string mode, DateTime? fromDate, DateTime? toDate);

    /// <summary>Dọn tự động (Quartz, xem SyncHistoryCleanupJob) — xoá SYNC_HISTORY (mọi đối tượng) cũ hơn
    /// <paramref name="retentionDays"/> ngày, Oracle tự tính cutoff bằng SYSTIMESTAMP.</summary>
    Task<int> DeleteOlderThanAsync(int retentionDays);

    /// <summary>Watchdog tự động (Quartz, xem SyncHistoryWatchdogJob) — các dòng đang STATUS = RUNNING kèm tần suất
    /// cấu hình của đối tượng (null nếu dòng không gắn SYNC_CONFIG) để watchdog tự tính ngưỡng "kẹt" riêng cho từng dòng.</summary>
    Task<IReadOnlyList<RunningSyncHistory>> GetRunningAsync();

    /// <summary>Đánh dấu FAILED cho 1 dòng CHỈ KHI còn RUNNING (không ghi đè lượt vừa hoàn tất). Trả về true nếu đã sửa.</summary>
    Task<bool> FailRunningAsync(string id, string errorMessage);
}

/// <summary>Dòng SYNC_HISTORY đang RUNNING kèm tần suất cấu hình — đầu vào tính ngưỡng của SyncHistoryWatchdogJob.</summary>
public class RunningSyncHistory
{
    public string Id { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string SyncType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int? FrequencyValue { get; set; }
    public string? FrequencyUnit { get; set; }
}
