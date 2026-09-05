namespace EvnHanoi.SyncService.Models;

/// <summary>Bảng SYNC_HISTORY (đã có sẵn từ trước) — 1 dòng/lần chạy đồng bộ (auto hoặc thủ công).</summary>
public class SyncHistory
{
    public string Id { get; set; } = string.Empty;
    public string SyncConfigId { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string SyncType { get; set; } = string.Empty; // AUTO | MANUAL
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty; // RUNNING | SUCCESS | FAILED | WARNING
    public int TotalRecords { get; set; }
    public int SuccessRecords { get; set; }
    public int FailedRecords { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CreatedBy { get; set; }
}

public static class SyncHistoryStatus
{
    public const string Running = "RUNNING";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";

    /// <summary>Bản ghi chính (Trạm/Đường dây/Thiết bị) đều lưu thành công, nhưng có ít nhất 1 cảnh báo ở
    /// bước phụ (đồng bộ tài liệu đính kèm) cần admin xem lại.</summary>
    public const string Warning = "WARNING";
}

public static class SyncType
{
    public const string Auto = "AUTO";
    public const string Manual = "MANUAL";
}
