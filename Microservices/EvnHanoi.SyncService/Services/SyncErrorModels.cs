namespace EvnHanoi.SyncService.Services;

/// <summary>Loại đối tượng của 1 dòng lỗi đồng bộ (PMIS_SYNC_ERROR.ENTITY_KIND).</summary>
public static class SyncErrorKinds
{
    public const string Infrastructure = "INFRA";
    public const string Equipment = "EQUIPMENT";
    public const string Document = "DOCUMENT";
    public const string File = "FILE";
}

public static class SyncErrorStates
{
    public const string Open = "OPEN";
    public const string Quarantined = "QUARANTINED";
    public const string Resolved = "RESOLVED";
}

/// <summary>Cấu hình ghi nhận/cách ly lỗi đồng bộ — section "Pmis:Errors" (biến môi trường Pmis__Errors__*).</summary>
public class PmisSyncErrorOptions
{
    public const string SectionName = "Pmis:Errors";

    /// <summary>Bật ghi nhận lỗi vào PMIS_SYNC_ERROR và cách ly (tắt = hành vi cũ: chỉ ghi SYNC_HISTORY_DETAIL).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Lỗi do dữ liệu nguồn gặp liên tiếp từng này lần thì bị CÁCH LY (không thử lại mỗi lượt).</summary>
    public int QuarantineAfter { get; set; } = 3;

    /// <summary>Thời gian cách ly lần đầu (phút); mỗi lần lỗi lại sau hạn thử lại thì nhân đôi, tối đa <see cref="QuarantineMaxMinutes"/>.</summary>
    public int QuarantineBaseMinutes { get; set; } = 1440;

    public int QuarantineMaxMinutes { get; set; } = 10080;
}

/// <summary>Trạng thái hiện tại của 1 mã đang có lỗi chưa giải quyết (OPEN hoặc QUARANTINED).</summary>
public sealed record SyncErrorState(string ErrorCode, string State, DateTime? NextRetryAt, string? SourceHash)
{
    /// <summary>true nếu mã đang bị cách ly, chưa tới hạn thử lại và hash nguồn KHÔNG đổi so với lúc lỗi → bỏ qua, không đẩy lại.</summary>
    public bool ShouldSkip(string? currentHash, DateTime nowUtc) =>
        State == SyncErrorStates.Quarantined
        && NextRetryAt is { } retry && retry > nowUtc
        && currentHash != null && string.Equals(SourceHash, currentHash, StringComparison.OrdinalIgnoreCase);
}

/// <summary>1 lần thất bại cần ghi vào PMIS_SYNC_ERROR.</summary>
public sealed record SyncErrorRecord(string Kind, string PmisCode, string ErrorCode, string? Message, string? SourceHash, string? SyncHistoryId);

/// <summary>1 dòng lỗi trả cho API/giao diện.</summary>
public class SyncErrorRow
{
    public string Id { get; set; } = string.Empty;
    public string EntityKind { get; set; } = string.Empty;
    public string PmisCode { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string State { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}

public class SyncErrorSummaryRow
{
    public string EntityKind { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Count { get; set; }
}
