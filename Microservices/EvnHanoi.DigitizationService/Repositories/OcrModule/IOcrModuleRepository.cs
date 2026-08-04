using EvnHanoi.DigitizationService.Models.Dto;
using EvnHanoi.DigitizationService.Models.OcrModule;

namespace EvnHanoi.DigitizationService.Repositories.OcrModule;

public interface IOcrModuleRepository
{
    Task CreateJobAsync(OcrModuleJob job);
    Task<OcrModuleJob?> GetJobByIdAsync(string jobId);
    Task UpdateJobStateAsync(string jobId, string state, int totalPages, string? errorMessage);
    Task InsertRegionsAsync(IReadOnlyList<OcrModuleRegion> regions);
    Task<int> CountRegionsAsync(string jobId);
    Task<PagedResult<OcrModuleRegionDto>> GetRegionsPagedAsync(string jobId, int page, int pageSize);
    Task<List<OcrModuleRegionDto>> GetAllRegionsAsync(string jobId);
    Task<List<OcrModuleRegion>> GetAllRegionEntitiesAsync(string jobId);
    Task UpdateRegionScriptTypesAsync(IReadOnlyDictionary<string, string> regionIdToScriptType);
    Task UpdateRegionFormulasAsync(IReadOnlyDictionary<string, string> regionIdToFormulaText);
    Task UpdateRegionsAsSignatureAsync(IReadOnlyDictionary<string, double> regionIdToScore);
    Task DeleteSealRegionsAsync(string jobId, int pageNumber);

    Task CreateTemplateSnapshotAsync(OcrModuleTemplateSnapshot snapshot);
    Task<List<OcrModuleTemplateSnapshot>> GetTemplateSnapshotsAsync(string? documentTypeCode);
    Task<OcrModuleTemplateSnapshot?> GetTemplateSnapshotByIdAsync(string id);
    Task InsertTemplateDiffResultsAsync(IReadOnlyList<OcrModuleTemplateDiffResult> results);
    Task<List<OcrModuleTemplateDiffResult>> GetTemplateDiffResultsAsync(string jobId);

    Task UpdateRegionSpellcheckSuggestionsAsync(IReadOnlyDictionary<string, string> regionIdToSuggestion);
    Task UpdateRegionSpellcheckStatusAsync(string regionId, string status, string? textRawOverride);

    Task ReplaceErrorAnalysisAsync(string jobId, IReadOnlyList<OcrModuleErrorAnalysis> errors, int? pageNumber = null);
    Task<List<OcrModuleErrorAnalysis>> GetErrorAnalysisAsync(string jobId);
    Task UpdateErrorAnalysisResolvedStatusAsync(string errorId, string resolvedStatus);
}
