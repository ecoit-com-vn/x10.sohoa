namespace EvnHanoi.DigitizationService.Models.OcrModule;

public class OcrModuleErrorAnalysis
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string? RegionId { get; set; }
    /// <summary>LowConfidence | SpellcheckRejected | TemplateMismatch | FormulaHeuristicMismatch | SealSignatureLowScore</summary>
    public string ErrorCategory { get; set; } = string.Empty;
    /// <summary>Low | Medium | High</summary>
    public string Severity { get; set; } = "Medium";
    public string? Detail { get; set; }
    /// <summary>Open | Resolved</summary>
    public string ResolvedStatus { get; set; } = "Open";
    public int PageNumber { get; set; }
}
