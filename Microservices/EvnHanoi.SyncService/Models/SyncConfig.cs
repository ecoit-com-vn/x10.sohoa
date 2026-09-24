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

    /// <summary>Điểm "tiếp tục" khi lượt trước dừng giữa chừng vì chạm giới hạn an toàn (xem
    /// Migration0011_AddSyncCursorToSyncConfig) — SUBSTATION/TRANSMISSION_LINE: chuỗi số "skip"; EQUIPMENT:
    /// mã PMIS của Trạm/Đường dây cha nơi ngân sách gọi PMIS thật bị dùng hết. NULL = lượt trước hoàn tất
    /// trọn vẹn, lượt sau bắt đầu lại từ đầu.</summary>
    public string? SyncCursor { get; set; }

    /// <summary>Điểm "tiếp tục" RIÊNG cho đồng bộ tài liệu đính kèm/ảnh QR của Trạm biến áp/Đường dây —
    /// độc lập với <see cref="SyncCursor"/> (vốn chỉ phục vụ phân trang dữ liệu chính), xem
    /// Migration0013_AddDocumentSyncCursorToSyncConfig. Giá trị là mã PMIS của owner nơi ngân sách
    /// MaxDocumentSyncCallsPerRun bị dùng hết ở lượt trước — lượt sau xoay vòng bắt đầu NGAY SAU mã này
    /// (xem PmisScheduledSyncJob.SyncDocumentsRotatingAsync). Chỉ có ý nghĩa với SUBSTATION/
    /// TRANSMISSION_LINE — EQUIPMENT không dùng cột này (đã tự xoay vòng qua SyncCursor từ trước, vì danh
    /// sách "cha" của Equipment lấy từ DB mình chứ không phải live PMIS pagination). NULL = lượt trước
    /// hoàn tất trọn vẹn, lượt sau bắt đầu lại từ đầu danh sách.</summary>
    public string? DocumentSyncCursor { get; set; }
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
