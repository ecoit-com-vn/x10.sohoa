namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// DTO cho upload file
/// </summary>
public record FileUploadDto
{
    public Guid? DocumentId { get; set; }
    public Guid FolderId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
}

/// <summary>
/// DTO cho khởi tạo chunked upload
/// </summary>
public record InitiateChunkedUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid FolderId { get; set; }
}

/// <summary>
/// DTO trả về sau khởi tạo upload
/// </summary>
public record InitiateChunkedUploadResponse
{
    public string UploadId { get; set; } = string.Empty;
    public int ChunkSize { get; set; }
    public int TotalChunks { get; set; }
}

/// <summary>
/// DTO cho upload chunk
/// </summary>
public record UploadChunkRequest
{
    public int ChunkNumber { get; set; }
    public string ETag { get; set; } = string.Empty;
}

/// <summary>
/// DTO cho hoàn tất upload
/// </summary>
public record CompleteChunkedUploadRequest
{
    public string UploadId { get; set; } = string.Empty;
    public List<UploadChunkRequest> Parts { get; set; } = new();
}

/// <summary>
/// DTO cho upload response
/// </summary>
public record FileUploadResponse
{
    public Guid DocumentVersionId { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "Active";
}

/// <summary>
/// DTO cho download token request
/// </summary>
public record DownloadTokenRequest
{
    public Guid VersionId { get; set; }
}

/// <summary>
/// DTO cho download token response
/// </summary>
public record DownloadTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; } = 60;
}

