using EvnHanoi.DigitizationService.Models.Dto;
using EvnHanoi.DigitizationService.Models.OcrModule;

namespace EvnHanoi.DigitizationService.Repositories.OcrModule;

public interface IOcrModuleRepository
{
    Task CreateJobAsync(OcrModuleJob job);
    Task<OcrModuleJob?> GetJobByIdAsync(string jobId);
    Task UpdateJobStateAsync(string jobId, string state, int totalPages, string? errorMessage);
    /// <summary>Cập nhật số trang đã OCR xong — không đổi State (vẫn Materializing), dùng để FE hiển thị %.</summary>
    Task UpdateJobProgressAsync(string jobId, int currentPage, int totalPages);
    /// <summary>Danh sách Job của màn hình "Quản lý dữ liệu huấn luyện AI-OCR" (SourceType=NewUpload).</summary>
    Task<PagedResult<OcrModuleJobListItemDto>> GetUploadedJobsPagedAsync(int page, int pageSize);
    /// <summary>Xóa mềm 1 Job huấn luyện AI-OCR. Trả về false nếu không tìm thấy.</summary>
    Task<bool> SoftDeleteJobAsync(string jobId);
    Task InsertRegionsAsync(IReadOnlyList<OcrModuleRegion> regions);
    Task<int> CountRegionsAsync(string jobId);
    Task<PagedResult<OcrModuleRegionDto>> GetRegionsPagedAsync(string jobId, int page, int pageSize);
    Task<List<OcrModuleRegionDto>> GetAllRegionsAsync(string jobId);
    Task<List<OcrModuleRegion>> GetAllRegionEntitiesAsync(string jobId);
    Task<OcrModuleRegion?> GetRegionByIdAsync(string regionId);
    Task<OcrModuleRegionDto?> GetRegionDtoByIdAsync(string regionId);
    Task UpdateRegionTextAndStatusAsync(string regionId, string textRaw, string? spellcheckStatus, string? editedBy);
    Task UpdateRegionScriptTypesAsync(IReadOnlyDictionary<string, string> regionIdToScriptType);
    Task UpdateRegionFormulasAsync(IReadOnlyDictionary<string, string> regionIdToFormulaText);
    Task ResetFormulaRegionsAsync(IReadOnlyList<string> regionIds);
    Task DeleteSealAndSignatureRegionsAsync(string jobId, int pageNumber);

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
