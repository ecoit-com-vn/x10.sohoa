namespace EvnHanoi.SyncService.Models;

/// <summary>
/// Bảng SYNC_HISTORY_DETAIL (đã có sẵn từ trước) — chính là "danh sách thông tin các bản ghi đã
/// đồng bộ tại lần đồng bộ đó" theo yêu cầu module 1/4. DataContent lưu nguyên JSON bản ghi PMIS.
/// </summary>
public class SyncHistoryDetail
{
    public string Id { get; set; } = string.Empty;
    public string SyncHistoryId { get; set; } = string.Empty;
    public string? SourceId { get; set; }
    public string? SourceCode { get; set; }
    public string? SourceName { get; set; }
    public string? TargetId { get; set; }
    public string ActionType { get; set; } = string.Empty; // CREATE | UPDATE | SKIP
    public string Status { get; set; } = string.Empty; // SUCCESS | FAILED | WARNING
    public string? DataContent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SyncTime { get; set; }
}

public static class SyncActionType
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Skip = "SKIP";
}

public static class SyncDetailStatus
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";

    /// <summary>Lỗi ở 1 bước phụ (vd. đồng bộ tài liệu đính kèm) — không tính là thất bại của cả bản ghi
    /// chính (Trạm/Đường dây/Thiết bị vẫn lưu thành công), chỉ cảnh báo để admin biết mà kiểm tra thêm.</summary>
    public const string Warning = "WARNING";
}
