namespace EvnHanoi.NotificationService.Models;

public class DocumentEsDocument
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentVersionId { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public string? FilePath { get; set; }
    public string? BucketName { get; set; }
    public string? DossierId { get; set; }
    public string? DossierTitle { get; set; }
    public string? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public long? UnitId { get; set; }
    public string? DossierTypeId { get; set; }
    public string? DossierTypeName { get; set; }
    public string? DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public int? StatusId { get; set; }
    public string? StatusCode { get; set; }
    public int? PublishStatusId { get; set; }
    public string? PublishStatusCode { get; set; }
    public List<string> EquipmentNames { get; set; } = new();
    public string? ExtractionSummary { get; set; }
    public DateTime? OcrCompletedAt { get; set; }
    public DateTime IndexedAt { get; set; }
    public bool IsDeleted { get; set; }
}
