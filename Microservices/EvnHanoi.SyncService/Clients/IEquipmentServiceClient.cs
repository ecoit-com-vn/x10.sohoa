using EvnHanoi.SyncService.Models.Internal;

namespace EvnHanoi.SyncService.Clients;

/// <summary>Gọi API nội bộ (internal/v1/...) của EquipmentService để lưu dữ liệu đã đồng bộ từ PMIS.</summary>
public interface IEquipmentServiceClient
{
    Task<List<UpsertInfrastructureFromPmisResult>> UpsertInfrastructureAsync(List<UpsertInfrastructureFromPmisRequest> items);
    Task<List<UpsertEquipmentFromPmisResult>> UpsertEquipmentAsync(List<UpsertEquipmentFromPmisRequest> items);

    /// <summary>Danh sách Trạm/Đường dây đã có PmisCode — dùng để lặp lấy thiết bị con khi auto-sync Thiết bị.</summary>
    Task<List<SyncedInfrastructurePmisCode>> GetSyncedInfrastructurePmisCodesAsync();

    Task<List<UpsertPmisDocumentResult>> UpsertDocumentsAsync(List<UpsertPmisDocumentRequest> items);

    /// <summary>Tài liệu PMIS đang chờ tải file vật lý (đã tới hạn thử lại) — cho PmisDocumentFileDownloadJob.</summary>
    Task<List<PendingPmisDocumentFile>> GetPendingDocumentFilesAsync(int take, IReadOnlyList<string>? excludePrefixes = null);

    /// <summary>Gửi kết quả tải 1 file (FileBase64) hoặc lý do lỗi (ErrorMessage) cho EquipmentService.</summary>
    Task AttachDocumentFileAsync(AttachPmisDocumentFileRequest request);

    /// <summary>Gửi file dạng LUỒNG (octet-stream, Content-Length biết trước) cho EquipmentService lưu MinIO + đánh dấu DONE —
    /// không base64 hoá, không nạp cả file vào RAM. Trả true nếu lưu xong.</summary>
    Task<bool> UploadDocumentFileAsync(string pmisDocumentCode, Stream content, long length, string sha256Hex, CancellationToken ct = default);

    /// <summary>Hỏi EquipmentService đã có file cùng SHA-256 (+ kích thước) chưa; nếu có thì gắn lại object cũ cho tài liệu này,
    /// đánh dấu DONE và trả true — không cần gửi/ghi file nữa (chống trùng nội dung).</summary>
    Task<bool> TryAttachExistingFileByHashAsync(string pmisDocumentCode, string sha256Hex, long length, CancellationToken ct = default);

    /// <summary>Ghi nhận lần tải lỗi: PERMANENT (tính vào số lần thử, backoff dài) hoặc TRANSIENT (không tính, thử lại sau ít phút).</summary>
    Task ReportDocumentFileFailureAsync(string pmisDocumentCode, bool transient, string? message, int transientRetryMinutes, CancellationToken ct = default);

    /// <summary>Số tài liệu đã có trong DB theo mã Trạm/Đường dây (gồm thiết bị con) — mốc bootstrap của job DOCUMENT.</summary>
    Task<Dictionary<string, int>> GetDocumentCountsByInfrastructureAsync();

    /// <summary>Tóm tắt hàng đợi tải file — cho PmisDocumentFileDownloadWatchdogJob.</summary>
    Task<PendingDocumentFileSummary> GetPendingDocumentSummaryAsync(IReadOnlyList<string>? excludePrefixes = null);

    /// <summary>Trạm/Đường dây còn tài liệu chưa có file (gồm cả tài liệu của thiết bị con) — cho backfill.</summary>
    Task<List<SyncedInfrastructurePmisCode>> GetPendingDocumentOwnersAsync();

    /// <summary>Trạng thái tải file hiện tại theo mã tài liệu (≤200) — cho màn Lịch sử đồng bộ.</summary>
    Task<List<PmisDocumentFileStatusDto>> GetDocumentFileStatusAsync(IReadOnlyCollection<string> codes);

    /// <summary>Số thiết bị bị đánh dấu "Đã chuyển TBA" bởi PMIS_SYNC trong <paramref name="sinceHours"/>
    /// giờ gần đây — xem PmisReconciliationJob.</summary>
    Task<int> GetRecentlyTransferredCountAsync(int sinceHours);
}

public class PmisDocumentFileStatusDto
{
    public string PmisDocumentCode { get; set; } = string.Empty;
    public string FileStatus { get; set; } = "NO_URL";
    public int FileAttempts { get; set; }
    public string? FileLastError { get; set; }
    public bool HasFile { get; set; }
}

public class SyncedInfrastructurePmisCode
{
    public string PmisCode { get; set; } = string.Empty;
    public int InfraTypeId { get; set; }
}
