namespace EvnHanoi.DigitizationService.Models.OcrModule;

public class OcrTrainingRetrainJob
{
    public string Id { get; set; } = string.Empty;
    public string? DatasetVersion { get; set; }
    /// <summary>Pending | Running | Completed | Failed</summary>
    public string Status { get; set; } = "Pending";
    public string? TriggeredBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class OcrTrainingDatasetVersion
{
    public string Id { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public string? ExportFilePath { get; set; }
    public string? ExportBucket { get; set; }
    public DateTime CreatedDate { get; set; }
}
