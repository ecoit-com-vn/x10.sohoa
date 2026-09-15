using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface IPmisApiCallLogRepository
{
    Task InsertAsync(PmisApiCallLog log);
    Task<(IEnumerable<PmisApiCallLog> Items, int TotalCount)> GetPagedAsync(string apiCode, int page, int pageSize);
    Task<int> DeleteOlderThanAsync(int retentionDays);

    /// <summary>Xoá thủ công theo yêu cầu admin (nút "Xoá lịch sử" trong dialog Lịch sử gọi API) — cùng 4
    /// chế độ với SyncHistoryRepository.DeleteAsync, luôn giới hạn theo <paramref name="apiCode"/> đang
    /// xem. <paramref name="mode"/>: "DATE_RANGE" (dùng fromDate/toDate), "KEEP_LAST_1_DAY"/
    /// "KEEP_LAST_7_DAYS" (giữ N ngày gần nhất, Oracle tự tính bằng SYSTIMESTAMP), "ALL" (xoá hết cho
    /// API này). Trả về số dòng đã xoá.</summary>
    Task<int> DeleteAsync(string apiCode, string mode, DateTime? fromDate, DateTime? toDate);
}
