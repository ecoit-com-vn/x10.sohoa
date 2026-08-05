using System.Data;
using Dapper;
using EvnHanoi.DigitizationService.Models.Dto;
using EvnHanoi.DigitizationService.Models.OcrModule;

namespace EvnHanoi.DigitizationService.Repositories.OcrModule;

public class OcrModuleRepository : IOcrModuleRepository
{
    private readonly IDbConnection _connection;

    public OcrModuleRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task CreateJobAsync(OcrModuleJob job)
    {
        var sql = $@"
            INSERT INTO OCR_MODULE_JOB (
                ID, SOURCE_TYPE, SOURCE_BUCKET, SOURCE_FILE_PATH, SOURCE_DOCUMENT_VERSION_ID,
                TOTAL_PAGES, STATE, CREATED_BY, CREATED_DATE
            ) VALUES (
                :{nameof(OcrModuleJob.Id)}, :{nameof(OcrModuleJob.SourceType)}, :{nameof(OcrModuleJob.SourceBucket)},
                :{nameof(OcrModuleJob.SourceFilePath)}, :{nameof(OcrModuleJob.SourceDocumentVersionId)},
                :{nameof(OcrModuleJob.TotalPages)}, :{nameof(OcrModuleJob.State)}, :{nameof(OcrModuleJob.CreatedBy)}, SYSTIMESTAMP
            )";

        await _connection.ExecuteAsync(sql, job);
    }

    public async Task<OcrModuleJob?> GetJobByIdAsync(string jobId)
    {
        var sql = $@"
            SELECT ID AS {nameof(OcrModuleJob.Id)}, SOURCE_TYPE AS {nameof(OcrModuleJob.SourceType)},
                   SOURCE_BUCKET AS {nameof(OcrModuleJob.SourceBucket)}, SOURCE_FILE_PATH AS {nameof(OcrModuleJob.SourceFilePath)},
                   SOURCE_DOCUMENT_VERSION_ID AS {nameof(OcrModuleJob.SourceDocumentVersionId)},
                   TOTAL_PAGES AS {nameof(OcrModuleJob.TotalPages)}, STATE AS {nameof(OcrModuleJob.State)},
                   ERROR_MESSAGE AS {nameof(OcrModuleJob.ErrorMessage)}, CREATED_DATE AS {nameof(OcrModuleJob.CreatedDate)}
            FROM OCR_MODULE_JOB WHERE ID = :JobId AND IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<OcrModuleJob>(sql, new { JobId = jobId });
    }

    public async Task UpdateJobStateAsync(string jobId, string state, int totalPages, string? errorMessage)
    {
        var sql = @"
            UPDATE OCR_MODULE_JOB
               SET STATE = :State, TOTAL_PAGES = :TotalPages, ERROR_MESSAGE = :ErrorMessage,
                   MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
             WHERE ID = :JobId";

        await _connection.ExecuteAsync(sql, new { JobId = jobId, State = state, TotalPages = totalPages, ErrorMessage = errorMessage });
    }

    public async Task InsertRegionsAsync(IReadOnlyList<OcrModuleRegion> regions)
    {
        if (regions.Count == 0) return;

        var sql = $@"
            INSERT INTO OCR_MODULE_REGION (
                ID, JOB_ID, PAGE_NUMBER, BOX_X0, BOX_Y0, BOX_X1, BOX_Y1,
                TEXT_RAW, CONFIDENCE, REGION_TYPE, SEAL_SIGNATURE_SCORE, STATUS, CREATED_DATE
            ) VALUES (
                :{nameof(OcrModuleRegion.Id)}, :{nameof(OcrModuleRegion.JobId)}, :{nameof(OcrModuleRegion.PageNumber)},
                :{nameof(OcrModuleRegion.BoxX0)}, :{nameof(OcrModuleRegion.BoxY0)}, :{nameof(OcrModuleRegion.BoxX1)}, :{nameof(OcrModuleRegion.BoxY1)},
                :{nameof(OcrModuleRegion.TextRaw)}, :{nameof(OcrModuleRegion.Confidence)}, :{nameof(OcrModuleRegion.RegionType)},
                :{nameof(OcrModuleRegion.SealSignatureScore)}, :{nameof(OcrModuleRegion.Status)}, SYSTIMESTAMP
            )";

        await _connection.ExecuteAsync(sql, regions);
    }

    public async Task<int> CountRegionsAsync(string jobId)
    {
        return await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM OCR_MODULE_REGION WHERE JOB_ID = :JobId AND IS_DELETED = 0",
            new { JobId = jobId });
    }

    public async Task<PagedResult<OcrModuleRegionDto>> GetRegionsPagedAsync(string jobId, int page, int pageSize)
    {
        var offset = (page - 1) * pageSize;

        var countSql = "SELECT COUNT(*) FROM OCR_MODULE_REGION WHERE JOB_ID = :JobId AND IS_DELETED = 0";
        var dataSql = $@"
            SELECT * FROM (
                SELECT {RegionSelectColumns()},
                       ROW_NUMBER() OVER (ORDER BY PAGE_NUMBER, BOX_Y0, BOX_X0) AS RN
                FROM OCR_MODULE_REGION WHERE JOB_ID = :JobId AND IS_DELETED = 0
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";

        var parameters = new { JobId = jobId, Offset = offset, OffsetPlusSize = offset + pageSize };

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, new { JobId = jobId });
        var items = (await _connection.QueryAsync<OcrModuleRegionDto>(dataSql, parameters)).ToList();

        return new PagedResult<OcrModuleRegionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<OcrModuleRegionDto>> GetAllRegionsAsync(string jobId)
    {
        var sql = $@"
            SELECT {RegionSelectColumns()}
            FROM OCR_MODULE_REGION WHERE JOB_ID = :JobId AND IS_DELETED = 0
            ORDER BY PAGE_NUMBER, BOX_Y0, BOX_X0";

        return (await _connection.QueryAsync<OcrModuleRegionDto>(sql, new { JobId = jobId })).ToList();
    }

    public async Task<List<OcrModuleRegion>> GetAllRegionEntitiesAsync(string jobId)
    {
        var sql = $@"
            SELECT ID AS {nameof(OcrModuleRegion.Id)}, JOB_ID AS {nameof(OcrModuleRegion.JobId)},
                   PAGE_NUMBER AS {nameof(OcrModuleRegion.PageNumber)},
                   BOX_X0 AS {nameof(OcrModuleRegion.BoxX0)}, BOX_Y0 AS {nameof(OcrModuleRegion.BoxY0)},
                   BOX_X1 AS {nameof(OcrModuleRegion.BoxX1)}, BOX_Y1 AS {nameof(OcrModuleRegion.BoxY1)},
                   TEXT_RAW AS {nameof(OcrModuleRegion.TextRaw)}, CONFIDENCE AS {nameof(OcrModuleRegion.Confidence)},
                   REGION_TYPE AS {nameof(OcrModuleRegion.RegionType)}, STATUS AS {nameof(OcrModuleRegion.Status)}
            FROM OCR_MODULE_REGION WHERE JOB_ID = :JobId AND IS_DELETED = 0
            ORDER BY PAGE_NUMBER, BOX_Y0, BOX_X0";

        return (await _connection.QueryAsync<OcrModuleRegion>(sql, new { JobId = jobId })).ToList();
    }

    public async Task UpdateRegionScriptTypesAsync(IReadOnlyDictionary<string, string> regionIdToScriptType)
    {
        if (regionIdToScriptType.Count == 0) return;

        var sql = @"
            UPDATE OCR_MODULE_REGION
               SET SCRIPT_TYPE = :ScriptType, MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
             WHERE ID = :RegionId";

        var updates = regionIdToScriptType.Select(kv => new { RegionId = kv.Key, ScriptType = kv.Value }).ToList();
        await _connection.ExecuteAsync(sql, updates);
    }

    public async Task UpdateRegionFormulasAsync(IReadOnlyDictionary<string, string> regionIdToFormulaText)
    {
        if (regionIdToFormulaText.Count == 0) return;

        var sql = @"
            UPDATE OCR_MODULE_REGION
               SET FORMULA_TEXT = :FormulaText, REGION_TYPE = 'Formula',
                   MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
             WHERE ID = :RegionId";

        var updates = regionIdToFormulaText.Select(kv => new { RegionId = kv.Key, FormulaText = kv.Value }).ToList();
        await _connection.ExecuteAsync(sql, updates);
    }

    /// <summary>Trả các vùng đã từng bị gắn nhãn Formula về lại Text — dùng khi chạy lại nhận diện công thức
    /// với logic mới không còn coi các vùng này là công thức nữa (vd. sau khi sửa heuristic loại trừ ngày/số
    /// trang), tránh nhãn Formula cũ bị kẹt lại vĩnh viễn dù không còn khớp tiêu chí hiện tại.</summary>
    public async Task ResetFormulaRegionsAsync(IReadOnlyList<string> regionIds)
    {
        if (regionIds.Count == 0) return;

        var sql = @"
            UPDATE OCR_MODULE_REGION
               SET REGION_TYPE = 'Text', FORMULA_TEXT = NULL,
                   MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
             WHERE ID = :RegionId";

        var updates = regionIds.Select(id => new { RegionId = id }).ToList();
        await _connection.ExecuteAsync(sql, updates);
    }

    /// <summary>Xóa mềm các vùng Seal/Signature đã nhận diện trước đó của 1 trang — tránh nhân đôi khi người dùng chạy lại phân tích cho đúng trang đó. Cả 2 loại đều là kết quả tự động nhận diện lại từ đầu mỗi lần chạy, không phải dữ liệu người dùng chỉnh sửa tay.</summary>
    public async Task DeleteSealAndSignatureRegionsAsync(string jobId, int pageNumber)
    {
        var sql = @"
            UPDATE OCR_MODULE_REGION
               SET IS_DELETED = 1, MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
             WHERE JOB_ID = :JobId AND PAGE_NUMBER = :PageNumber AND REGION_TYPE IN ('Seal', 'Signature')";

        await _connection.ExecuteAsync(sql, new { JobId = jobId, PageNumber = pageNumber });
    }

    public async Task CreateTemplateSnapshotAsync(OcrModuleTemplateSnapshot snapshot)
    {
        var sql = $@"
            INSERT INTO OCR_MODULE_TEMPLATE_SNAPSHOT (
                ID, NAME, DOCUMENT_TYPE_CODE, SOURCE_JOB_ID, REFERENCE_REGIONS_JSON, CREATED_BY, CREATED_DATE
            ) VALUES (
                :{nameof(OcrModuleTemplateSnapshot.Id)}, :{nameof(OcrModuleTemplateSnapshot.Name)},
                :{nameof(OcrModuleTemplateSnapshot.DocumentTypeCode)}, :{nameof(OcrModuleTemplateSnapshot.SourceJobId)},
                :{nameof(OcrModuleTemplateSnapshot.ReferenceRegionsJson)}, :{nameof(OcrModuleTemplateSnapshot.CreatedBy)}, SYSTIMESTAMP
            )";

        await _connection.ExecuteAsync(sql, snapshot);
    }

    public async Task<List<OcrModuleTemplateSnapshot>> GetTemplateSnapshotsAsync(string? documentTypeCode)
    {
        var where = string.IsNullOrWhiteSpace(documentTypeCode) ? "WHERE IS_DELETED = 0" : "WHERE IS_DELETED = 0 AND DOCUMENT_TYPE_CODE = :DocumentTypeCode";
        var sql = $@"
            SELECT ID AS {nameof(OcrModuleTemplateSnapshot.Id)}, NAME AS {nameof(OcrModuleTemplateSnapshot.Name)},
                   DOCUMENT_TYPE_CODE AS {nameof(OcrModuleTemplateSnapshot.DocumentTypeCode)},
                   SOURCE_JOB_ID AS {nameof(OcrModuleTemplateSnapshot.SourceJobId)},
                   CREATED_BY AS {nameof(OcrModuleTemplateSnapshot.CreatedBy)}, CREATED_DATE AS {nameof(OcrModuleTemplateSnapshot.CreatedDate)}
            FROM OCR_MODULE_TEMPLATE_SNAPSHOT {where}
            ORDER BY CREATED_DATE DESC";

        return (await _connection.QueryAsync<OcrModuleTemplateSnapshot>(sql, new { DocumentTypeCode = documentTypeCode })).ToList();
    }

    public async Task<OcrModuleTemplateSnapshot?> GetTemplateSnapshotByIdAsync(string id)
    {
        var sql = $@"
            SELECT ID AS {nameof(OcrModuleTemplateSnapshot.Id)}, NAME AS {nameof(OcrModuleTemplateSnapshot.Name)},
                   DOCUMENT_TYPE_CODE AS {nameof(OcrModuleTemplateSnapshot.DocumentTypeCode)},
                   SOURCE_JOB_ID AS {nameof(OcrModuleTemplateSnapshot.SourceJobId)},
                   REFERENCE_REGIONS_JSON AS {nameof(OcrModuleTemplateSnapshot.ReferenceRegionsJson)},
                   CREATED_BY AS {nameof(OcrModuleTemplateSnapshot.CreatedBy)}, CREATED_DATE AS {nameof(OcrModuleTemplateSnapshot.CreatedDate)}
            FROM OCR_MODULE_TEMPLATE_SNAPSHOT WHERE ID = :Id AND IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<OcrModuleTemplateSnapshot>(sql, new { Id = id });
    }

    public async Task InsertTemplateDiffResultsAsync(IReadOnlyList<OcrModuleTemplateDiffResult> results)
    {
        if (results.Count == 0) return;

        var sql = $@"
            INSERT INTO OCR_MODULE_TEMPLATE_DIFF_RESULT (
                ID, JOB_ID, TEMPLATE_SNAPSHOT_ID, REGION_ID, PAGE_NUMBER, DIFF_TYPE, DETAIL, STATUS, CREATED_DATE
            ) VALUES (
                :{nameof(OcrModuleTemplateDiffResult.Id)}, :{nameof(OcrModuleTemplateDiffResult.JobId)},
                :{nameof(OcrModuleTemplateDiffResult.TemplateSnapshotId)}, :{nameof(OcrModuleTemplateDiffResult.RegionId)},
                :{nameof(OcrModuleTemplateDiffResult.PageNumber)}, :{nameof(OcrModuleTemplateDiffResult.DiffType)},
                :{nameof(OcrModuleTemplateDiffResult.Detail)}, :{nameof(OcrModuleTemplateDiffResult.Status)}, SYSTIMESTAMP
            )";

        await _connection.ExecuteAsync(sql, results);
    }

    public async Task<List<OcrModuleTemplateDiffResult>> GetTemplateDiffResultsAsync(string jobId)
    {
        var sql = $@"
            SELECT ID AS {nameof(OcrModuleTemplateDiffResult.Id)}, JOB_ID AS {nameof(OcrModuleTemplateDiffResult.JobId)},
                   TEMPLATE_SNAPSHOT_ID AS {nameof(OcrModuleTemplateDiffResult.TemplateSnapshotId)},
                   REGION_ID AS {nameof(OcrModuleTemplateDiffResult.RegionId)}, PAGE_NUMBER AS {nameof(OcrModuleTemplateDiffResult.PageNumber)},
                   DIFF_TYPE AS {nameof(OcrModuleTemplateDiffResult.DiffType)}, DETAIL AS {nameof(OcrModuleTemplateDiffResult.Detail)},
                   STATUS AS {nameof(OcrModuleTemplateDiffResult.Status)}
            FROM OCR_MODULE_TEMPLATE_DIFF_RESULT WHERE JOB_ID = :JobId AND IS_DELETED = 0
            ORDER BY PAGE_NUMBER";

        return (await _connection.QueryAsync<OcrModuleTemplateDiffResult>(sql, new { JobId = jobId })).ToList();
    }

    public async Task UpdateRegionSpellcheckSuggestionsAsync(IReadOnlyDictionary<string, string> regionIdToSuggestion)
    {
        if (regionIdToSuggestion.Count == 0) return;

        var sql = @"
            UPDATE OCR_MODULE_REGION
               SET SPELLCHECK_SUGGESTION = :Suggestion, SPELLCHECK_STATUS = 'Pending',
                   MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
             WHERE ID = :RegionId";

        var updates = regionIdToSuggestion.Select(kv => new { RegionId = kv.Key, Suggestion = kv.Value }).ToList();
        await _connection.ExecuteAsync(sql, updates);
    }

    public async Task UpdateRegionSpellcheckStatusAsync(string regionId, string status, string? textRawOverride)
    {
        var sql = textRawOverride != null
            ? @"UPDATE OCR_MODULE_REGION
                   SET SPELLCHECK_STATUS = :Status, TEXT_RAW = :TextRawOverride,
                       MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
                 WHERE ID = :RegionId"
            : @"UPDATE OCR_MODULE_REGION
                   SET SPELLCHECK_STATUS = :Status,
                       MODIFIED_DATE = SYSTIMESTAMP, ROW_VERSION = ROW_VERSION + 1
                 WHERE ID = :RegionId";

        await _connection.ExecuteAsync(sql, new { RegionId = regionId, Status = status, TextRawOverride = textRawOverride });
    }

    public async Task ReplaceErrorAnalysisAsync(string jobId, IReadOnlyList<OcrModuleErrorAnalysis> errors, int? pageNumber = null)
    {
        // pageNumber = null → thay toàn bộ lỗi của Job (hành vi cũ); có giá trị → chỉ thay lỗi của đúng trang đó,
        // giữ nguyên lỗi đã tổng hợp trước đó ở các trang khác.
        await _connection.ExecuteAsync(
            "UPDATE OCR_MODULE_ERROR_ANALYSIS SET IS_DELETED = 1 WHERE JOB_ID = :JobId AND (:PageNumber IS NULL OR PAGE_NUMBER = :PageNumber)",
            new { JobId = jobId, PageNumber = pageNumber });

        if (errors.Count == 0) return;

        var sql = $@"
            INSERT INTO OCR_MODULE_ERROR_ANALYSIS (
                ID, JOB_ID, REGION_ID, PAGE_NUMBER, ERROR_CATEGORY, SEVERITY, DETAIL, RESOLVED_STATUS, CREATED_DATE
            ) VALUES (
                :{nameof(OcrModuleErrorAnalysis.Id)}, :{nameof(OcrModuleErrorAnalysis.JobId)}, :{nameof(OcrModuleErrorAnalysis.RegionId)},
                :{nameof(OcrModuleErrorAnalysis.PageNumber)}, :{nameof(OcrModuleErrorAnalysis.ErrorCategory)},
                :{nameof(OcrModuleErrorAnalysis.Severity)}, :{nameof(OcrModuleErrorAnalysis.Detail)},
                :{nameof(OcrModuleErrorAnalysis.ResolvedStatus)}, SYSTIMESTAMP
            )";

        await _connection.ExecuteAsync(sql, errors);
    }

    public async Task<List<OcrModuleErrorAnalysis>> GetErrorAnalysisAsync(string jobId)
    {
        var sql = $@"
            SELECT ID AS {nameof(OcrModuleErrorAnalysis.Id)}, JOB_ID AS {nameof(OcrModuleErrorAnalysis.JobId)},
                   REGION_ID AS {nameof(OcrModuleErrorAnalysis.RegionId)}, PAGE_NUMBER AS {nameof(OcrModuleErrorAnalysis.PageNumber)},
                   ERROR_CATEGORY AS {nameof(OcrModuleErrorAnalysis.ErrorCategory)}, SEVERITY AS {nameof(OcrModuleErrorAnalysis.Severity)},
                   DETAIL AS {nameof(OcrModuleErrorAnalysis.Detail)}, RESOLVED_STATUS AS {nameof(OcrModuleErrorAnalysis.ResolvedStatus)}
            FROM OCR_MODULE_ERROR_ANALYSIS WHERE JOB_ID = :JobId AND IS_DELETED = 0
            ORDER BY PAGE_NUMBER";

        return (await _connection.QueryAsync<OcrModuleErrorAnalysis>(sql, new { JobId = jobId })).ToList();
    }

    public async Task UpdateErrorAnalysisResolvedStatusAsync(string errorId, string resolvedStatus)
    {
        await _connection.ExecuteAsync(
            "UPDATE OCR_MODULE_ERROR_ANALYSIS SET RESOLVED_STATUS = :Status, MODIFIED_DATE = SYSTIMESTAMP WHERE ID = :Id",
            new { Id = errorId, Status = resolvedStatus });
    }

    private static string RegionSelectColumns() => $@"
        ID AS {nameof(OcrModuleRegionDto.Id)}, PAGE_NUMBER AS {nameof(OcrModuleRegionDto.PageNumber)},
        BOX_X0 AS {nameof(OcrModuleRegionDto.BoxX0)}, BOX_Y0 AS {nameof(OcrModuleRegionDto.BoxY0)},
        BOX_X1 AS {nameof(OcrModuleRegionDto.BoxX1)}, BOX_Y1 AS {nameof(OcrModuleRegionDto.BoxY1)},
        TEXT_RAW AS {nameof(OcrModuleRegionDto.TextRaw)}, CONFIDENCE AS {nameof(OcrModuleRegionDto.Confidence)},
        SCRIPT_TYPE AS {nameof(OcrModuleRegionDto.ScriptType)}, REGION_TYPE AS {nameof(OcrModuleRegionDto.RegionType)},
        FORMULA_TEXT AS {nameof(OcrModuleRegionDto.FormulaText)}, SEAL_SIGNATURE_SCORE AS {nameof(OcrModuleRegionDto.SealSignatureScore)},
        SPELLCHECK_SUGGESTION AS {nameof(OcrModuleRegionDto.SpellcheckSuggestion)}, SPELLCHECK_STATUS AS {nameof(OcrModuleRegionDto.SpellcheckStatus)},
        STATUS AS {nameof(OcrModuleRegionDto.Status)}";
}
