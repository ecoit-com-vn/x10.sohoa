using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Repositories;

/// <summary>Bảng PMIS_SYNC_ERROR (Migration0018) — mỗi bản ghi lỗi đồng bộ 1 dòng theo (loại, mã PMIS, mã lỗi). Mọi mốc thời gian ghi bằng
/// <see cref="DateTime.UtcNow"/> (cùng hệ quy chiếu với PMIS_SYNC_STATE).</summary>
public interface IPmisSyncErrorRepository
{
    /// <summary>Các mã (trong <paramref name="codes"/>) đang có lỗi chưa giải quyết. Mã có nhiều lỗi: ưu tiên dòng QUARANTINED.</summary>
    Task<Dictionary<string, SyncErrorState>> GetUnresolvedAsync(string kind, IReadOnlyCollection<string> codes);

    /// <summary>Ghi nhận các lần thất bại (MERGE: tăng số lần, mở lại nếu đã RESOLVED) rồi cách ly những dòng đủ điều kiện
    /// (mã lỗi cách ly được và số lần ≥ <paramref name="options"/>.QuarantineAfter).</summary>
    Task RecordFailuresAsync(IReadOnlyCollection<SyncErrorRecord> failures, PmisSyncErrorOptions options);

    /// <summary>Đánh dấu RESOLVED các lỗi chưa giải quyết của những mã vừa đồng bộ thành công.</summary>
    Task ResolveAsync(string kind, IReadOnlyCollection<string> codes);

    Task<List<SyncErrorSummaryRow>> GetSummaryAsync();

    Task<(List<SyncErrorRow> Items, int Total)> ListAsync(string? kind, string? errorCode, string? state, string? keyword, int page, int pageSize);

    /// <summary>Mở lại để thử lại ngay (STATE=OPEN, NEXT_RETRY_AT=NULL, OCCURRENCES=0 để phải thất bại đủ QuarantineAfter lần nữa mới bị cách ly lại) cho các dòng khớp bộ lọc. Trả số dòng đã mở lại.</summary>
    Task<int> RetryAsync(string? kind, string? errorCode, string? pmisCode);
}
