using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DocumentDigitizationRepository : IDocumentDigitizationRepository
{
    private readonly IDbConnection _connection;

    public DocumentDigitizationRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<DocumentOcrProgress?> GetProgressByVersionIdAsync(Guid documentVersionId)
    {
        EnsureOpen();
        const string sql = @"
            SELECT ID, DOCUMENT_ID AS DocumentId, DOCUMENT_VERSION_ID AS DocumentVersionId,
                   ACTION, PHASE, CURRENT_PAGE AS CurrentPage, TOTAL_PAGES AS TotalPages,
                   PROGRESS, STATUS, PROCESS_OPTION AS ProcessOption,
                   BUCKET_NAME AS BucketName, FILE_PATH AS FilePath, FORM_JSON AS FormJson,
                   ERROR_MESSAGE AS ErrorMessage, CREATED_BY AS CreatedBy, CREATED_DATE AS CreatedDate,
                   MODIFIED_BY AS ModifiedBy, MODIFIED_DATE AS ModifiedDate, IS_DELETED AS IsDeleted
            FROM DOCUMENT_OCR_PROGRESS
            WHERE DOCUMENT_VERSION_ID = :VersionId AND IS_DELETED = 0
            ORDER BY CREATED_DATE DESC
            FETCH FIRST 1 ROWS ONLY";
        return await _connection.QuerySingleOrDefaultAsync<DocumentOcrProgress>(
            sql, new { VersionId = documentVersionId.ToString() });
    }

    public async Task<DocumentOcrProgress?> GetProgressByIdAsync(Guid id)
    {
        EnsureOpen();
        const string sql = @"
            SELECT ID, DOCUMENT_ID AS DocumentId, DOCUMENT_VERSION_ID AS DocumentVersionId,
                   ACTION, PHASE, CURRENT_PAGE AS CurrentPage, TOTAL_PAGES AS TotalPages,
                   PROGRESS, STATUS, PROCESS_OPTION AS ProcessOption,
                   BUCKET_NAME AS BucketName, FILE_PATH AS FilePath, FORM_JSON AS FormJson,
                   ERROR_MESSAGE AS ErrorMessage, CREATED_BY AS CreatedBy, CREATED_DATE AS CreatedDate,
                   MODIFIED_BY AS ModifiedBy, MODIFIED_DATE AS ModifiedDate, IS_DELETED AS IsDeleted
            FROM DOCUMENT_OCR_PROGRESS
            WHERE ID = :Id AND IS_DELETED = 0";
        return await _connection.QuerySingleOrDefaultAsync<DocumentOcrProgress>(
            sql, new { Id = id.ToString() });
    }

    public async Task<Guid> CreateProgressAsync(DocumentOcrProgress progress)
    {
        EnsureOpen();
        if (progress.Id == Guid.Empty)
            progress.Id = Guid.Parse(UuidHelper.NewUuid());

        const string sql = @"
            INSERT INTO DOCUMENT_OCR_PROGRESS (
                ID, DOCUMENT_ID, DOCUMENT_VERSION_ID, ACTION, PHASE,
                CURRENT_PAGE, TOTAL_PAGES, PROGRESS, STATUS, PROCESS_OPTION,
                BUCKET_NAME, FILE_PATH, FORM_JSON, ERROR_MESSAGE,
                CREATED_BY, CREATED_DATE, IS_DELETED
            ) VALUES (
                :Id, :DocumentId, :DocumentVersionId, :Action, :Phase,
                :CurrentPage, :TotalPages, :Progress, :Status, :ProcessOption,
                :BucketName, :FilePath, :FormJson, :ErrorMessage,
                :CreatedBy, :CreatedDate, :IsDeleted
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = progress.Id.ToString(),
            DocumentId = progress.DocumentId.ToString(),
            DocumentVersionId = progress.DocumentVersionId.ToString(),
            progress.Action,
            progress.Phase,
            progress.CurrentPage,
            progress.TotalPages,
            progress.Progress,
            progress.Status,
            progress.ProcessOption,
            progress.BucketName,
            progress.FilePath,
            FormJson = OracleClob.Param(progress.FormJson),
            progress.ErrorMessage,
            progress.CreatedBy,
            progress.CreatedDate,
            IsDeleted = progress.IsDeleted ? 1 : 0
        });

        return progress.Id;
    }

    public async Task<bool> UpdateProgressAsync(DocumentOcrProgress progress)
    {
        EnsureOpen();
        const string sql = @"
            UPDATE DOCUMENT_OCR_PROGRESS SET
                ACTION = :Action,
                PHASE = :Phase,
                CURRENT_PAGE = :CurrentPage,
                TOTAL_PAGES = :TotalPages,
                PROGRESS = :Progress,
                STATUS = :Status,
                PROCESS_OPTION = :ProcessOption,
                FORM_JSON = :FormJson,
                ERROR_MESSAGE = :ErrorMessage,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = :ModifiedDate
            WHERE ID = :Id AND IS_DELETED = 0";

        var rows = await _connection.ExecuteAsync(sql, new
        {
            Id = progress.Id.ToString(),
            progress.Action,
            progress.Phase,
            progress.CurrentPage,
            progress.TotalPages,
            progress.Progress,
            progress.Status,
            progress.ProcessOption,
            FormJson = OracleClob.Param(progress.FormJson),
            progress.ErrorMessage,
            progress.ModifiedBy,
            ModifiedDate = progress.ModifiedDate ?? DateTime.UtcNow
        });
        return rows > 0;
    }

    public async Task<DocumentExtractionResult?> GetExtractionResultByVersionIdAsync(Guid documentVersionId, Guid? equipmentId = null)
    {
        EnsureOpen();
        const string sql = @"
            SELECT ID, DOCUMENT_ID AS DocumentId, DOCUMENT_VERSION_ID AS DocumentVersionId,
                   OCR_PROGRESS_ID AS OcrProgressId, STATUS, RESULT_JSON AS ResultJson,
                   RESULT_FILE_PATH AS ResultFilePath, BUCKET_NAME AS BucketName,
                   FORM_JSON AS FormJson, MERGED_DATA_JSON AS MergedDataJson,
                   ERROR_MESSAGE AS ErrorMessage, CREATED_BY AS CreatedBy, CREATED_DATE AS CreatedDate,
                   MODIFIED_BY AS ModifiedBy, MODIFIED_DATE AS ModifiedDate, IS_DELETED AS IsDeleted,
                   EQUIPMENT_ID AS EquipmentId
            FROM DOCUMENT_EXTRACTION_RESULTS
            WHERE DOCUMENT_VERSION_ID = :VersionId 
              AND ((:EquipmentId IS NULL AND EQUIPMENT_ID IS NULL) OR (EQUIPMENT_ID = :EquipmentId))
              AND IS_DELETED = 0
            ORDER BY CREATED_DATE DESC
            FETCH FIRST 1 ROWS ONLY";
        return await _connection.QuerySingleOrDefaultAsync<DocumentExtractionResult>(
            sql, new { VersionId = documentVersionId.ToString(), EquipmentId = equipmentId?.ToString() });
    }

    public async Task<Guid> CreateExtractionResultAsync(DocumentExtractionResult result)
    {
        EnsureOpen();
        if (result.Id == Guid.Empty)
            result.Id = Guid.Parse(UuidHelper.NewUuid());

        const string sql = @"
            INSERT INTO DOCUMENT_EXTRACTION_RESULTS (
                ID, DOCUMENT_ID, DOCUMENT_VERSION_ID, OCR_PROGRESS_ID, STATUS,
                RESULT_JSON, RESULT_FILE_PATH, BUCKET_NAME, FORM_JSON, MERGED_DATA_JSON,
                ERROR_MESSAGE, CREATED_BY, CREATED_DATE, IS_DELETED, EQUIPMENT_ID
            ) VALUES (
                :Id, :DocumentId, :DocumentVersionId, :OcrProgressId, :Status,
                :ResultJson, :ResultFilePath, :BucketName, :FormJson, :MergedDataJson,
                :ErrorMessage, :CreatedBy, :CreatedDate, :IsDeleted, :EquipmentId
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = result.Id.ToString(),
            DocumentId = result.DocumentId.ToString(),
            DocumentVersionId = result.DocumentVersionId.ToString(),
            OcrProgressId = result.OcrProgressId?.ToString(),
            result.Status,
            ResultJson = OracleClob.Param(result.ResultJson),
            result.ResultFilePath,
            result.BucketName,
            FormJson = OracleClob.Param(result.FormJson),
            MergedDataJson = OracleClob.Param(result.MergedDataJson),
            result.ErrorMessage,
            result.CreatedBy,
            result.CreatedDate,
            IsDeleted = result.IsDeleted ? 1 : 0,
            EquipmentId = result.EquipmentId?.ToString()
        });

        return result.Id;
    }

    public async Task<bool> UpdateExtractionResultAsync(DocumentExtractionResult result)
    {
        EnsureOpen();
        const string sql = @"
            UPDATE DOCUMENT_EXTRACTION_RESULTS SET
                STATUS = :Status,
                RESULT_JSON = :ResultJson,
                RESULT_FILE_PATH = :ResultFilePath,
                BUCKET_NAME = :BucketName,
                FORM_JSON = :FormJson,
                MERGED_DATA_JSON = :MergedDataJson,
                ERROR_MESSAGE = :ErrorMessage,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = :ModifiedDate,
                EQUIPMENT_ID = :EquipmentId
            WHERE ID = :Id AND IS_DELETED = 0";

        var rows = await _connection.ExecuteAsync(sql, new
        {
            Id = result.Id.ToString(),
            result.Status,
            ResultJson = OracleClob.Param(result.ResultJson),
            result.ResultFilePath,
            result.BucketName,
            FormJson = OracleClob.Param(result.FormJson),
            MergedDataJson = OracleClob.Param(result.MergedDataJson),
            result.ErrorMessage,
            result.ModifiedBy,
            ModifiedDate = result.ModifiedDate ?? DateTime.UtcNow,
            EquipmentId = result.EquipmentId?.ToString()
        });
        return rows > 0;
    }

    public async Task<(IEnumerable<EvnHanoi.EquipmentService.Core.DTOs.OcrJobListItemDto> items, int totalCount)> GetJobsPagedAsync(
        EvnHanoi.EquipmentService.Core.DTOs.OcrJobListFilter filter)
    {
        EnsureOpen();

        var conditions = new List<string> { "ocr.IS_DELETED = 0" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("ocr.STATUS = :Status");
            parameters.Add("Status", filter.Status);
        }
        if (!string.IsNullOrWhiteSpace(filter.Phase))
        {
            conditions.Add("ocr.PHASE = :Phase");
            parameters.Add("Phase", filter.Phase);
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            conditions.Add("LOWER(d.NAME) LIKE :Keyword");
            parameters.Add("Keyword", $"%{filter.Keyword.ToLower().Trim()}%");
        }
        if (filter.FromDate.HasValue)
        {
            conditions.Add("ocr.CREATED_DATE >= :FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }
        if (filter.ToDate.HasValue)
        {
            conditions.Add("ocr.CREATED_DATE <= :ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        var whereClause = string.Join(" AND ", conditions);

        var countSql = $@"
            SELECT COUNT(1)
            FROM DOCUMENT_OCR_PROGRESS ocr
            JOIN DOCUMENTS d ON d.ID = ocr.DOCUMENT_ID
            WHERE {whereClause}";

        var totalCount = Convert.ToInt32(await _connection.ExecuteScalarAsync(countSql, parameters));
        if (totalCount == 0)
        {
            return (Enumerable.Empty<EvnHanoi.EquipmentService.Core.DTOs.OcrJobListItemDto>(), 0);
        }

        var offset = (filter.Page - 1) * filter.PageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", filter.PageSize);

        var listSql = $@"
            SELECT
                ocr.ID AS ProgressId,
                ocr.DOCUMENT_ID AS DocumentId,
                ocr.DOCUMENT_VERSION_ID AS DocumentVersionId,
                d.NAME AS DocumentName,
                dt.NAME AS DocumentTypeName,
                d.DOSSIER_ID AS DossierId,
                infra.NAME AS DossierInfrastructureName,
                infra.CODE AS DossierInfrastructureCode,
                ext.EQUIPMENT_ID AS EquipmentId,
                eq.Name AS EquipmentName,
                ocr.PHASE AS Phase,
                ocr.STATUS AS Status,
                ocr.PROGRESS AS Progress,
                ocr.CURRENT_PAGE AS CurrentPage,
                ocr.TOTAL_PAGES AS TotalPages,
                ocr.ERROR_MESSAGE AS ErrorMessage,
                ocr.CREATED_DATE AS CreatedDate,
                ocr.MODIFIED_DATE AS ModifiedDate
            FROM DOCUMENT_OCR_PROGRESS ocr
            JOIN DOCUMENTS d ON d.ID = ocr.DOCUMENT_ID
            LEFT JOIN DOCUMENT_TYPES dt ON dt.ID = d.DOCUMENT_TYPE_ID
            LEFT JOIN DOSSIERS dos ON dos.Id = d.DOSSIER_ID
            LEFT JOIN INFRASTRUCTURE infra ON infra.ID = dos.InfrastructureId
            LEFT JOIN DOCUMENT_EXTRACTION_RESULTS ext ON ext.OCR_PROGRESS_ID = ocr.ID AND ext.IS_DELETED = 0
            LEFT JOIN Equipments eq ON eq.Id = ext.EQUIPMENT_ID
            WHERE {whereClause}
            ORDER BY ocr.CREATED_DATE DESC
            OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        var items = await _connection.QueryAsync<EvnHanoi.EquipmentService.Core.DTOs.OcrJobListItemDto>(listSql, parameters);
        return (items, totalCount);
    }

    private void EnsureOpen() => _connection.EnsureOpen();
}
