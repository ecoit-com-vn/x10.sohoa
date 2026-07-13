namespace EvnHanoi.EquipmentService.Core.DTOs;

// ===== FOLDER DTOs =====

/// <summary>
/// DTO đại diện cho một thư mục tài liệu trong cây thư mục (flat structure từ API)
/// </summary>
public class FolderNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public long UnitId { get; set; }  // ORGANIZATION_UNIT.ID is NUMBER (long)
    public string UnitCode { get; set; } = string.Empty;  // ORGANIZATION_UNIT.CODE — dùng cho MinIO path
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int RowVersion { get; set; }
}

/// <summary>
/// DTO tạo thư mục mới
/// </summary>
public class CreateFolderDto
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
}

/// <summary>
/// DTO cập nhật thư mục
/// </summary>
public class UpdateFolderDto
{
    public string Name { get; set; } = string.Empty;
    public int RowVersion { get; set; }
}

/// <summary>
/// Tài liệu trong thư mục kèm đường dẫn file MinIO — dùng nén ZIP thư mục.
/// </summary>
public class FolderZipDocumentDto
{
    public string DocumentName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

// ===== DOCUMENT DTOs =====

/// <summary>
/// DTO đại diện cho một tài liệu trong danh sách tài liệu
/// </summary>
public class DocumentListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? FolderId { get; set; }
    public Guid? DossierId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public Guid? LatestVersionId { get; set; }
    public int RowVersion { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public bool IsEquipmentProfile { get; set; }

    /// <summary>Tiến trình OCR/extraction — chỉ populate khi list theo hồ sơ.</summary>
    public DocumentOcrProgressSummaryDto? OcrProgress { get; set; }
    public DocumentExtractionResultSummaryDto? ExtractionResult { get; set; }
}

/// <summary>Tóm tắt OCR trên danh sách tài liệu (không kèm FORM_JSON).</summary>
public class DocumentOcrProgressSummaryDto
{
    public Guid Id { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int Progress { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProcessOption { get; set; }
}

/// <summary>Tóm tắt kết quả bóc tách trên danh sách (không kèm RESULT_JSON).</summary>
public class DocumentExtractionResultSummaryDto
{
    public Guid Id { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO tạo tài liệu mới
/// </summary>
public class CreateDocumentDto
{
    public string Name { get; set; } = string.Empty;
    public Guid? FolderId { get; set; }
    public Guid? DossierId { get; set; }
}

/// <summary>
/// DTO cập nhật tài liệu
/// </summary>
public class UpdateDocumentDto
{
    public string Name { get; set; } = string.Empty;
    public int RowVersion { get; set; }
}

// ===== DOCUMENT VERSION DTOs =====

/// <summary>
/// DTO chi tiết phiên bản tài liệu
/// </summary>
public class DocumentVersionDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public int UploadSource { get; set; }  // 1: Thư mục, 2: Scan, 3: Web
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsDeleted { get; set; }
}

// ===== FILTER / QUERY =====

/// <summary>
/// DTO lọc danh sách tài liệu trong một thư mục
/// </summary>
public class DocumentFilterDto
{
    public Guid? FolderId { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// ===== DOSSIER CATALOG TREE DTOs =====

public class FolderCatalogNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public long UnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class UnitQueryDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class DossierTypeQueryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? GridTypeId { get; set; }
}

public class InfrastructureQueryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int InfraTypeId { get; set; }
    public int? GridTypeId { get; set; }
}

public class ActiveDossierQueryDto
{
    public string Id { get; set; } = string.Empty;
    public string? InfrastructureId { get; set; }
    public string DossierTypeId { get; set; } = string.Empty;
    public string? DossierSetId { get; set; }
    public string? FormDataJson { get; set; }
    public string DossierTypeName { get; set; } = string.Empty;
    public string? DossierSetName { get; set; }
    public int? GridTypeId { get; set; }
}

/// <summary>
/// DTO chứa gợi ý chỉ mục phiên bản tài liệu OCR
/// </summary>
public class DocumentOcrIndexHintDto
{
    public Guid VersionId { get; set; }
    public string? BucketName { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int TotalPages { get; set; }
}


