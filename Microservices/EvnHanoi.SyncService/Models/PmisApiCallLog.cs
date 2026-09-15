namespace EvnHanoi.SyncService.Models;

/// <summary>1 dòng lịch sử gọi PMIS thật — xem Migration0008_CreatePmisApiCallLogTable.</summary>
public class PmisApiCallLog
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? RequestPayload { get; set; }
    public int? StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public string? HttpClientName { get; set; }
    public DateTime CalledAt { get; set; }

    /// <summary>Số bản ghi (Items.Count) trả về — chỉ có ở API dạng danh sách gọi thành công, null với
    /// API không phải danh sách (ChiTietThietBi, AnhQRCode) hoặc khi gọi lỗi.</summary>
    public int? RecordCount { get; set; }
}

/// <summary>Body cho nút "Xoá lịch sử" trong dialog Lịch sử gọi API — cùng 4 chế độ với
/// CleanupSyncHistoryRequest (SyncHistoryController), xem PmisApiCallLogRepository.DeleteAsync.</summary>
public class CleanupPmisApiCallLogRequest
{
    /// <summary>"DATE_RANGE" | "KEEP_LAST_1_DAY" | "KEEP_LAST_7_DAYS" | "ALL".</summary>
    public string Mode { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
