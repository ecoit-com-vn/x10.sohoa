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

    /// <summary>INFRASTRUCTURE | EQUIPMENT | DOCUMENT — xem SyncRecordKind. NULL cho dữ liệu lịch sử cũ
    /// (trước Migration0014), API/FE coi NULL như thuộc tab chính đang xem.</summary>
    public string? RecordKind { get; set; }
}

/// <summary>SyncHistoryDetail kèm trạng thái tải file HIỆN TẠI của tài liệu (chỉ điền khi RecordKind=DOCUMENT) —
/// tính lúc đọc (SyncHistoryController.GetItems), không lưu vào bảng, vì trạng thái đổi sau khi lượt đồng bộ xong.</summary>
public class SyncHistoryDetailView : SyncHistoryDetail
{
    public string? FileStatus { get; set; }
    public int? FileAttempts { get; set; }
    public string? FileLastError { get; set; }
    public bool? HasFile { get; set; }
}

public static class SyncActionType
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Skip = "SKIP";
}

/// <summary>Phân loại 1 dòng SYNC_HISTORY_DETAIL — xem Migration0014_AddRecordKindToSyncHistoryDetail.</summary>
public static class SyncRecordKind
{
    public const string Infrastructure = "INFRASTRUCTURE";
    public const string Equipment = "EQUIPMENT";
    public const string Document = "DOCUMENT";
}

public static class SyncDetailStatus
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";

    /// <summary>Lỗi ở 1 bước phụ (vd. đồng bộ tài liệu đính kèm) — không tính là thất bại của cả bản ghi
    /// chính (Trạm/Đường dây/Thiết bị vẫn lưu thành công), chỉ cảnh báo để admin biết mà kiểm tra thêm.</summary>
    public const string Warning = "WARNING";
}
