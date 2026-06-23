namespace EvnHanoi.EquipmentService.Core.DTOs;

public class DossierDocumentFilterDto
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class MoveDocumentsFromFolderRequest
{
    public List<Guid> DocumentIds { get; set; } = new();
    public Guid DocumentTypeId { get; set; }
}

public class MovedDossierDocumentDto
{
    public Guid DocumentId { get; set; }
    public Guid VersionId { get; set; }
    public string Name { get; set; } = string.Empty;
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
