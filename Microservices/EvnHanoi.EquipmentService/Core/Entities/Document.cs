namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Phiên bản tài liệu — lưu lịch sử tất cả phiên bản của một tài liệu
/// </summary>
public class DocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public int UploadSource { get; set; }  // 1: Thư mục, 2: Scan, 3: Web, 4: Xử lý tự động (khử nhiễu...)
    public string? FilePath { get; set; }  // Đường dẫn MinIO
    public string? MinioVersionId { get; set; }  // ID phiên bản lưu trên MinIO
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    /// <summary>Số trang: PDF = thực tế; ảnh = 1; loại khác = 0.</summary>
    public int PageCount { get; set; }

    // Upload tracking (added for file upload system)
    public string? FileHash { get; set; }  // SHA256 for integrity check
    public Guid? UploadSessionId { get; set; }  // Link to upload session
    public int ChunksCount { get; set; } = 1;  // 1 = direct upload, >1 = chunked upload

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}

/// <summary>
/// Chữ ký số của tài liệu
/// </summary>
public class DocumentSignature
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public string? SignerName { get; set; }
    public DateTime SignDate { get; set; }
    public string? Issuer { get; set; }
    public bool IsValid { get; set; } = true;
}

/// <summary>
/// Phiên làm việc upload file — theo dõi trang thái upload chunked
/// </summary>
public class UploadSession
{
    public Guid Id { get; set; }
    public string UploadId { get; set; } = string.Empty;  // Unique token for this upload session
    public Guid? FolderId { get; set; }
    public Guid? DossierId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; } = 0;
    public string Status { get; set; } = "InProgress";  // InProgress, Completed, Failed, Expired
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Audit fields
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}