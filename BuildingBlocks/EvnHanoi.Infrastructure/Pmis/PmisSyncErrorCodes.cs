namespace EvnHanoi.Infrastructure.Pmis;

/// <summary>
/// Mã lỗi đồng bộ PMIS ỔN ĐỊNH, dùng chung giữa EquipmentService (nơi phát sinh lỗi) và SyncService (nơi ghi nhận/cách ly) — thay cho việc
/// phân tích chuỗi thông báo tiếng Việt. Mỗi mã có 2 tính chất: có thể CÁCH LY (lỗi do dữ liệu nguồn, thử lại vô ích tới khi PMIS đổi dữ
/// liệu) hay KHÔNG (lỗi tạm thời/hạ tầng, luôn phải thử lại).
/// </summary>
public static class PmisSyncErrorCodes
{
    /// <summary>Thiết bị PMIS thiếu cấp điện áp (capDienAp) và trạm cha không có loại lưới → không xác định được loại thiết bị.</summary>
    public const string MissingVoltage = "MISSING_VOLTAGE";

    /// <summary>PMIS không trả mã loại thiết bị (maLoaiTB).</summary>
    public const string MissingTypeCode = "MISSING_TYPE_CODE";

    /// <summary>Không tìm thấy Trạm/Đường dây cha của thiết bị (chưa đồng bộ tới hoặc mã lệch). KHÔNG cách ly: hash nguồn của thiết bị không đổi khi
    /// trạm cha được đồng bộ xong, nên cách ly sẽ chặn thử lại dù nguyên nhân đã hết.</summary>
    public const string ParentNotFound = "PARENT_NOT_FOUND";

    /// <summary>Mã PMIS (hoặc Code trong cùng trạm) đã được dùng cho 1 thiết bị khác đang hoạt động.</summary>
    public const string DuplicateActiveCode = "DUPLICATE_ACTIVE_CODE";

    /// <summary>Chuyển TBA tự động xung đột với thao tác khác (thử lại ở lượt sau).</summary>
    public const string TransferConflict = "TRANSFER_CONFLICT";

    /// <summary>Không tìm thấy đối tượng sở hữu tài liệu.</summary>
    public const string OwnerNotFound = "OWNER_NOT_FOUND";

    /// <summary>Lỗi lưu trữ/hạ tầng (MinIO, kết nối...), tạm thời.</summary>
    public const string StorageError = "STORAGE_ERROR";

    /// <summary>Chưa phân loại (ngoại lệ không xác định) — coi như tạm thời.</summary>
    public const string Unknown = "UNKNOWN";

    /// <summary>true nếu lỗi do dữ liệu nguồn và nên được CÁCH LY sau nhiều lần liên tiếp (không thử lại mỗi lượt).</summary>
    public static bool IsQuarantinable(string? code) => code is MissingVoltage or MissingTypeCode or DuplicateActiveCode;

    /// <summary>Chuẩn hoá mã (null/rỗng → UNKNOWN).</summary>
    public static string Normalize(string? code) => string.IsNullOrWhiteSpace(code) ? Unknown : code.Trim().ToUpperInvariant();
}
