namespace EvnHanoi.SyncService.Models;

/// <summary>Bảng SYNC_CONFIG (đã có sẵn từ trước — không phải bảng mới của tính năng này).</summary>
public class SyncConfig
{
    public string Id { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int FrequencyValue { get; set; }
    public string FrequencyUnit { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? NextSyncAt { get; set; }
    public int RowVersion { get; set; }

    /// <summary>Số lần đồng bộ tự động lỗi liên tiếp ngay từ bước gọi danh sách PMIS — reset về 0 khi
    /// có 1 lượt chạy xong bình thường (kể cả Failed vì 0/n item thành công). Dùng để backoff tăng dần
    /// và quyết định khi nào cảnh báo admin — xem PmisScheduledSyncJob.</summary>
    public int ConsecutiveFailureCount { get; set; }
}

public class UpdateSyncConfigRequest
{
    public bool IsEnabled { get; set; }
    public int FrequencyValue { get; set; }
    public string FrequencyUnit { get; set; } = "MINUTE";
    public int RowVersion { get; set; }
}

/// <summary>3 đối tượng đồng bộ cố định — khớp CHECK constraint CK_SYNC_CONFIG_OBJECT_TYPE.</summary>
public static class SyncObjectType
{
    public const string Substation = "SUBSTATION";
    public const string TransmissionLine = "TRANSMISSION_LINE";
    public const string Equipment = "EQUIPMENT";

    public static bool IsValid(string value) =>
        value is Substation or TransmissionLine or Equipment;
}
