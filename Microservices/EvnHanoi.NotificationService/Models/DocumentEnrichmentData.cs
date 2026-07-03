namespace EvnHanoi.NotificationService.Models;

public class DocumentEnrichmentData
{
    public string DocumentVersionId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string? DossierId { get; set; }
    public string? DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public bool DocumentIsDeleted { get; set; }
    public int? StatusId { get; set; }
    public string? StatusCode { get; set; }
    public string? DossierTypeId { get; set; }
    public string? DossierTypeName { get; set; }
    public string? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public long? UnitId { get; set; }
    public int? PublishStatusId { get; set; }
    public string? PublishStatusCode { get; set; }
    public string? MergedDataJson { get; set; }
    public DateTime? OcrCompletedAt { get; set; }
    public string? BucketName { get; set; }
}
