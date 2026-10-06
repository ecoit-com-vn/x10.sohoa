namespace EvnHanoi.EquipmentService.Core.DTOs;

public class DossierDocumentFilterDto
{
    public string? Keyword { get; set; }
    /// <summary>Lọc theo 1 loại văn bản cụ thể — null = lấy mọi loại (giữ nguyên hành vi cũ). Dùng cho
    /// giao diện cây thư mục loại văn bản ở tab Tài liệu đính kèm.</summary>
    public Guid? DocumentTypeId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class MoveDocumentsFromFolderRequest
{
    public List<Guid> DocumentIds { get; set; } = new();
    public Guid DocumentTypeId { get; set; }
    public List<Guid>? EquipmentIds { get; set; }
}

public class MovedDossierDocumentDto
{
    public Guid DocumentId { get; set; }
    public Guid VersionId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>"Chọn từ kho PMIS" — COPY tài liệu đã đồng bộ từ PMIS (PMIS_DOCUMENT) vào hồ sơ, khác hẳn
/// MoveDocumentsFromFolderRequest (MOVE, xoá file gốc): tài liệu PMIS phải giữ nguyên để dùng lại được
/// cho hồ sơ khác.</summary>
public class CopyDocumentsFromPmisRequest
{
    public List<Guid> PmisDocumentIds { get; set; } = new();
    public Guid DocumentTypeId { get; set; }
}

public class DossierDocumentSnapshotItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public Guid? LatestVersionId { get; set; }
}

public class InitiateDossierChunkedUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid? DocumentTypeId { get; set; }
}
