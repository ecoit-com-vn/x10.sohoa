using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly IDbConnection _connection;

    private const string DocumentCreatorJoin =
        @"LEFT JOIN APP_USER cu ON (
            LOWER(cu.Id) = LOWER(d.CREATED_BY)
            OR LOWER(cu.UserName) = LOWER(d.CREATED_BY)
            OR LOWER(cu.UserName) = LOWER(d.CREATOR_NAME)
        )";

    private const string DocumentCreatedByNameSelect =
        "NVL(cu.FullName, NVL(d.CREATOR_NAME, d.CREATED_BY)) AS CreatedByName";

    public DocumentRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    // ===== FOLDER OPERATIONS =====

    public async Task<IEnumerable<FolderNodeDto>> GetFolderTreeByUnitAsync(long unitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                f.ID,
                f.NAME,
                f.PARENT_ID AS ParentId,
                f.UNIT_ID AS UnitId,
                ou.CODE AS UnitCode,
                f.CREATED_BY AS CreatedBy,
                f.CREATED_DATE AS CreatedDate,
                f.MODIFIED_BY AS ModifiedBy,
                f.MODIFIED_DATE AS ModifiedDate
            FROM FOLDERS f
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = f.UNIT_ID
            WHERE f.UNIT_ID = :UnitId 
              AND f.IS_DELETED = 0
            ORDER BY f.NAME ASC";

        return await _connection.QueryAsync<FolderNodeDto>(sql, new { UnitId = unitId });
    }

    public async Task<FolderNodeDto?> GetFolderByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                f.ID,
                f.NAME,
                f.PARENT_ID AS ParentId,
                f.UNIT_ID AS UnitId,
                ou.CODE AS UnitCode,
                f.CREATED_BY AS CreatedBy,
                f.CREATED_DATE AS CreatedDate,
                f.MODIFIED_BY AS ModifiedBy,
                f.MODIFIED_DATE AS ModifiedDate
            FROM FOLDERS f
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = f.UNIT_ID
            WHERE f.ID = :Id 
              AND f.IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<FolderNodeDto>(sql, new { Id = id.ToString() });
    }

    public async Task<Guid> CreateFolderAsync(Folder folder)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var id = Guid.NewGuid();
        folder.Id = id;

        var sql = @"
            INSERT INTO FOLDERS (
                ID,
                NAME,
                PARENT_ID,
                UNIT_ID,
                ROW_VERSION,
                CREATED_BY,
                CREATED_DATE,
                IS_DELETED
            ) VALUES (
                :Id,
                :Name,
                :ParentId,
                :UnitId,
                1,
                :CreatedBy,
                SYSTIMESTAMP,
                0
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            folder.Name,
            ParentId = folder.ParentId?.ToString(),
            folder.UnitId,
            folder.CreatedBy
        });

        return id;
    }

    public async Task<bool> UpdateFolderAsync(Folder folder)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE FOLDERS
            SET 
                NAME = :Name,
                ROW_VERSION = ROW_VERSION + 1,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE ID = :Id 
              AND ROW_VERSION = :ExpectedVersion
              AND IS_DELETED = 0";

        var rowsAffected = await _connection.ExecuteAsync(sql, new
        {
            folder.Name,
            folder.ModifiedBy,
            Id = folder.Id.ToString(),
            ExpectedVersion = folder.RowVersion
        });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteFolderAsync(Guid id, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE FOLDERS
            SET 
                IS_DELETED = 1,
                ROW_VERSION = ROW_VERSION + 1,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE ID = :Id 
              AND IS_DELETED = 0";

        var rowsAffected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            ModifiedBy = modifiedBy
        });

        return rowsAffected > 0;
    }

    public async Task<bool> FolderExistsAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = "SELECT COUNT(*) FROM FOLDERS WHERE ID = :Id AND IS_DELETED = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id.ToString() });
        return count > 0;
    }

    // ===== DOCUMENT OPERATIONS =====

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByFolderAsync(Guid? folderId, DocumentFilterDto filter)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var whereClause = "IS_DELETED = 0";
        var aliasedWhereClause = "d.IS_DELETED = 0";
        
        if (folderId.HasValue)
        {
            whereClause += " AND FOLDER_ID = :FolderId";
            aliasedWhereClause += " AND d.FOLDER_ID = :FolderId";
        }
        else
        {
            whereClause += " AND FOLDER_ID IS NULL";
            aliasedWhereClause += " AND d.FOLDER_ID IS NULL";
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            whereClause += " AND NAME LIKE :Keyword";
            aliasedWhereClause += " AND d.NAME LIKE :Keyword";
        }

        // Count total
        var countSql = $"SELECT COUNT(*) FROM DOCUMENTS WHERE {whereClause}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(
            countSql,
            new { FolderId = folderId?.ToString(), Keyword = $"%{filter.Keyword}%" }
        );

        // Get paged data (join phiên bản mới nhất để lấy FILE_SIZE, MIME_TYPE)
        var offset = (filter.Page - 1) * filter.PageSize;
        var listSql = $@"
            SELECT 
                d.ID,
                d.NAME,
                d.FOLDER_ID AS FolderId,
                d.DOSSIER_ID AS DossierId,
                d.CREATED_BY AS CreatedBy,
                {DocumentCreatedByNameSelect},
                d.CREATED_DATE AS CreatedDate,
                NVL(latest.FILE_SIZE, 0) AS FileSize,
                latest.MIME_TYPE AS MimeType,
                latest.LATEST_VERSION_ID AS LatestVersionId
            FROM DOCUMENTS d
            {DocumentCreatorJoin}
            LEFT JOIN (
                SELECT dv.DOCUMENT_ID, dv.ID AS LATEST_VERSION_ID, dv.FILE_SIZE, dv.MIME_TYPE
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN (
                    SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                    FROM DOCUMENT_VERSIONS
                    WHERE IS_DELETED = 0
                    GROUP BY DOCUMENT_ID
                ) mx ON mx.DOCUMENT_ID = dv.DOCUMENT_ID AND mx.MAX_VER = dv.VERSION_NUMBER
                WHERE dv.IS_DELETED = 0
            ) latest ON latest.DOCUMENT_ID = d.ID
            WHERE {aliasedWhereClause}
            ORDER BY d.CREATED_DATE DESC, d.NAME ASC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var items = await _connection.QueryAsync<DocumentListItemDto>(
            listSql,
            new { FolderId = folderId?.ToString(), Keyword = $"%{filter.Keyword}%", Offset = offset, PageSize = filter.PageSize }
        );

        return (items, totalCount);
    }

    public async Task<DocumentListItemDto?> GetDocumentByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                d.ID,
                d.NAME,
                d.FOLDER_ID AS FolderId,
                d.DOSSIER_ID AS DossierId,
                d.DOCUMENT_TYPE_ID AS DocumentTypeId,
                dt.NAME AS DocumentTypeName,
                d.CREATED_BY AS CreatedBy,
                {DocumentCreatedByNameSelect},
                d.CREATED_DATE AS CreatedDate,
                NVL(latest.FILE_SIZE, 0) AS FileSize,
                latest.MIME_TYPE AS MimeType,
                latest.LATEST_VERSION_ID AS LatestVersionId
            FROM DOCUMENTS d
            {DocumentCreatorJoin}
            LEFT JOIN DOCUMENT_TYPES dt ON dt.ID = d.DOCUMENT_TYPE_ID AND dt.IsDeleted = 0
            LEFT JOIN (
                SELECT dv.DOCUMENT_ID, dv.ID AS LATEST_VERSION_ID, dv.FILE_SIZE, dv.MIME_TYPE
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN (
                    SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                    FROM DOCUMENT_VERSIONS
                    WHERE IS_DELETED = 0
                    GROUP BY DOCUMENT_ID
                ) mx ON mx.DOCUMENT_ID = dv.DOCUMENT_ID AND mx.MAX_VER = dv.VERSION_NUMBER
                WHERE dv.IS_DELETED = 0
            ) latest ON latest.DOCUMENT_ID = d.ID
            WHERE d.ID = :Id 
              AND d.IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<DocumentListItemDto>(sql, new { Id = id.ToString() });
    }

    public async Task<Guid> CreateDocumentAsync(Document document)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var id = Guid.NewGuid();
        document.Id = id;

        var sql = @"
            INSERT INTO DOCUMENTS (
                ID,
                NAME,
                FOLDER_ID,
                DOSSIER_ID,
                DOCUMENT_TYPE_ID,
                ROW_VERSION,
                CREATED_BY,
                CREATOR_NAME,
                CREATED_DATE,
                IS_DELETED
            ) VALUES (
                :Id,
                :Name,
                :FolderId,
                :DossierId,
                :DocumentTypeId,
                1,
                :CreatedBy,
                :CreatorName,
                SYSTIMESTAMP,
                0
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            document.Name,
            FolderId = document.FolderId?.ToString(),
            DossierId = document.DossierId?.ToString(),
            DocumentTypeId = document.DocumentTypeId?.ToString(),
            document.CreatedBy,
            document.CreatorName
        });

        return id;
    }

    public async Task<bool> UpdateDocumentAsync(Document document)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE DOCUMENTS
            SET 
                NAME = :Name,
                ROW_VERSION = ROW_VERSION + 1,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE ID = :Id 
              AND ROW_VERSION = :ExpectedVersion
              AND IS_DELETED = 0";

        var rowsAffected = await _connection.ExecuteAsync(sql, new
        {
            document.Name,
            document.ModifiedBy,
            Id = document.Id.ToString(),
            ExpectedVersion = document.RowVersion
        });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE DOCUMENTS
            SET 
                IS_DELETED = 1,
                ROW_VERSION = ROW_VERSION + 1,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE ID = :Id 
              AND IS_DELETED = 0";

        var rowsAffected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            ModifiedBy = modifiedBy
        });

        return rowsAffected > 0;
    }

    // ===== DOCUMENT VERSION OPERATIONS =====

    public async Task<Guid> CreateDocumentVersionAsync(DocumentVersion version)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var id = Guid.NewGuid();
        version.Id = id;

        var sql = @"
            INSERT INTO DOCUMENT_VERSIONS (
                ID,
                DOCUMENT_ID,
                VERSION_NUMBER,
                UPLOAD_SOURCE,
                FILE_PATH,
                FILE_SIZE,
                MIME_TYPE,
                CREATED_BY,
                CREATED_DATE,
                IS_DELETED
            ) VALUES (
                :Id,
                :DocumentId,
                :VersionNumber,
                :UploadSource,
                :FilePath,
                :FileSize,
                :MimeType,
                :CreatedBy,
                SYSTIMESTAMP,
                0
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            DocumentId = version.DocumentId.ToString(),
            version.VersionNumber,
            version.UploadSource,
            version.FilePath,
            version.FileSize,
            version.MimeType,
            version.CreatedBy
        });

        return id;
    }

    public async Task<IEnumerable<DocumentVersionDto>> GetDocumentVersionsAsync(Guid documentId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                ID,
                DOCUMENT_ID AS DocumentId,
                VERSION_NUMBER AS VersionNumber,
                UPLOAD_SOURCE AS UploadSource,
                FILE_PATH AS FilePath,
                FILE_SIZE AS FileSize,
                MIME_TYPE AS MimeType,
                CREATED_BY AS CreatedBy,
                CREATED_DATE AS CreatedDate
            FROM DOCUMENT_VERSIONS
            WHERE DOCUMENT_ID = :DocumentId 
              AND IS_DELETED = 0
            ORDER BY VERSION_NUMBER DESC";

        return await _connection.QueryAsync<DocumentVersionDto>(sql, new { DocumentId = documentId.ToString() });
    }

    public async Task<DocumentVersionDto?> GetDocumentVersionByIdAsync(Guid versionId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                ID,
                DOCUMENT_ID AS DocumentId,
                VERSION_NUMBER AS VersionNumber,
                UPLOAD_SOURCE AS UploadSource,
                FILE_PATH AS FilePath,
                FILE_SIZE AS FileSize,
                MIME_TYPE AS MimeType,
                CREATED_BY AS CreatedBy,
                CREATED_DATE AS CreatedDate
            FROM DOCUMENT_VERSIONS
            WHERE ID = :Id 
              AND IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<DocumentVersionDto>(sql, new { Id = versionId.ToString() });
    }

    // ===== UPLOAD SESSION OPERATIONS =====

    public async Task<Guid> CreateUploadSessionAsync(UploadSession session)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var id = Guid.NewGuid();
        session.Id = id;

        var sql = @"
            INSERT INTO UPLOAD_SESSIONS (
                ID,
                UPLOAD_ID,
                FOLDER_ID,
                DOSSIER_ID,
                FILE_NAME,
                TOTAL_CHUNKS,
                COMPLETED_CHUNKS,
                STATUS,
                CREATED_DATE,
                EXPIRES_AT,
                CREATED_BY,
                IS_DELETED
            ) VALUES (
                :Id,
                :UploadId,
                :FolderId,
                :DossierId,
                :FileName,
                :TotalChunks,
                :CompletedChunks,
                :Status,
                SYSTIMESTAMP,
                :ExpiresAt,
                :CreatedBy,
                0
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            session.UploadId,
            FolderId = session.FolderId?.ToString(),
            DossierId = session.DossierId?.ToString(),
            session.FileName,
            session.TotalChunks,
            session.CompletedChunks,
            session.Status,
            session.ExpiresAt,
            session.CreatedBy
        });

        return id;
    }

    public async Task<UploadSession?> GetUploadSessionAsync(string uploadId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                ID,
                UPLOAD_ID AS UploadId,
                FOLDER_ID AS FolderId,
                DOSSIER_ID AS DossierId,
                FILE_NAME AS FileName,
                TOTAL_CHUNKS AS TotalChunks,
                COMPLETED_CHUNKS AS CompletedChunks,
                STATUS,
                CREATED_DATE AS CreatedDate,
                EXPIRES_AT AS ExpiresAt,
                CREATED_BY AS CreatedBy,
                MODIFIED_BY AS ModifiedBy,
                MODIFIED_DATE AS ModifiedDate,
                IS_DELETED AS IsDeleted
            FROM UPLOAD_SESSIONS
            WHERE UPLOAD_ID = :UploadId 
              AND IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<UploadSession>(sql, new { UploadId = uploadId });
    }

    public async Task<bool> UpdateUploadSessionAsync(UploadSession session)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE UPLOAD_SESSIONS
            SET 
                COMPLETED_CHUNKS = :CompletedChunks,
                STATUS = :Status,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE ID = :Id 
              AND IS_DELETED = 0";

        var rowsAffected = await _connection.ExecuteAsync(sql, new
        {
            session.CompletedChunks,
            session.Status,
            session.ModifiedBy,
            Id = session.Id.ToString()
        });

        return rowsAffected > 0;
    }

    public async Task<bool> CompleteUploadSessionAsync(string uploadId, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE UPLOAD_SESSIONS
            SET 
                STATUS = 'Completed',
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE UPLOAD_ID = :UploadId 
              AND IS_DELETED = 0";

        var rowsAffected = await _connection.ExecuteAsync(sql, new
        {
            UploadId = uploadId,
            ModifiedBy = modifiedBy
        });

        return rowsAffected > 0;
    }

    public async Task<int> DeleteExpiredUploadSessionsAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE UPLOAD_SESSIONS
            SET 
                IS_DELETED = 1,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE EXPIRES_AT < SYSTIMESTAMP 
              AND IS_DELETED = 0";

        return await _connection.ExecuteAsync(sql);
    }

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByDossierAsync(
        Guid dossierId,
        DossierDocumentFilterDto filter)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var aliasedWhere = "d.IS_DELETED = 0 AND d.DOSSIER_ID = :DossierId";
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            aliasedWhere += " AND d.NAME LIKE :Keyword";

        var countSql = $"SELECT COUNT(*) FROM DOCUMENTS d WHERE {aliasedWhere}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, new
        {
            DossierId = dossierId.ToString(),
            Keyword = $"%{filter.Keyword}%"
        });

        var offset = (filter.Page - 1) * filter.PageSize;
        var listSql = $@"
            SELECT 
                d.ID,
                d.NAME,
                d.FOLDER_ID AS FolderId,
                d.DOSSIER_ID AS DossierId,
                d.DOCUMENT_TYPE_ID AS DocumentTypeId,
                dt.NAME AS DocumentTypeName,
                d.CREATED_BY AS CreatedBy,
                {DocumentCreatedByNameSelect},
                d.CREATED_DATE AS CreatedDate,
                NVL(latest.FILE_SIZE, 0) AS FileSize,
                latest.MIME_TYPE AS MimeType,
                latest.LATEST_VERSION_ID AS LatestVersionId,
                ocr.ID AS OcrProgressId,
                ocr.DOCUMENT_VERSION_ID AS OcrDocumentVersionId,
                ocr.PHASE AS OcrPhase,
                ocr.CURRENT_PAGE AS OcrCurrentPage,
                ocr.TOTAL_PAGES AS OcrTotalPages,
                ocr.PROGRESS AS OcrProgress,
                ocr.STATUS AS OcrStatus,
                ocr.PROCESS_OPTION AS OcrProcessOption,
                ext.ID AS ExtractionResultId,
                ext.DOCUMENT_VERSION_ID AS ExtractionDocumentVersionId,
                ext.STATUS AS ExtractionStatus
            FROM DOCUMENTS d
            {DocumentCreatorJoin}
            LEFT JOIN DOCUMENT_TYPES dt ON dt.ID = d.DOCUMENT_TYPE_ID AND dt.IsDeleted = 0
            LEFT JOIN (
                SELECT dv.DOCUMENT_ID, dv.ID AS LATEST_VERSION_ID, dv.FILE_SIZE, dv.MIME_TYPE
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN (
                    SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                    FROM DOCUMENT_VERSIONS
                    WHERE IS_DELETED = 0
                    GROUP BY DOCUMENT_ID
                ) mx ON mx.DOCUMENT_ID = dv.DOCUMENT_ID AND mx.MAX_VER = dv.VERSION_NUMBER
                WHERE dv.IS_DELETED = 0
            ) latest ON latest.DOCUMENT_ID = d.ID
            LEFT JOIN (
                SELECT ID, DOCUMENT_VERSION_ID, PHASE, CURRENT_PAGE, TOTAL_PAGES, PROGRESS, STATUS, PROCESS_OPTION
                FROM (
                    SELECT p.ID, p.DOCUMENT_VERSION_ID, p.PHASE, p.CURRENT_PAGE, p.TOTAL_PAGES, p.PROGRESS,
                           p.STATUS, p.PROCESS_OPTION,
                           ROW_NUMBER() OVER (PARTITION BY p.DOCUMENT_VERSION_ID ORDER BY p.CREATED_DATE DESC) AS RN
                    FROM DOCUMENT_OCR_PROGRESS p
                    WHERE p.IS_DELETED = 0
                ) ranked WHERE RN = 1
            ) ocr ON ocr.DOCUMENT_VERSION_ID = latest.LATEST_VERSION_ID
            LEFT JOIN (
                SELECT ID, DOCUMENT_VERSION_ID, STATUS
                FROM (
                    SELECT e.ID, e.DOCUMENT_VERSION_ID, e.STATUS,
                           ROW_NUMBER() OVER (PARTITION BY e.DOCUMENT_VERSION_ID ORDER BY e.CREATED_DATE DESC) AS RN
                    FROM DOCUMENT_EXTRACTION_RESULTS e
                    WHERE e.IS_DELETED = 0
                ) ranked WHERE RN = 1
            ) ext ON ext.DOCUMENT_VERSION_ID = latest.LATEST_VERSION_ID
            WHERE {aliasedWhere}
            ORDER BY d.CREATED_DATE DESC, d.NAME ASC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rows = await _connection.QueryAsync<DossierDocumentListRow>(listSql, new
        {
            DossierId = dossierId.ToString(),
            Keyword = $"%{filter.Keyword}%",
            Offset = offset,
            PageSize = filter.PageSize
        });

        var items = rows.Select(MapDossierDocumentListRow);
        return (items, totalCount);
    }

    private static DocumentListItemDto MapDossierDocumentListRow(DossierDocumentListRow row)
    {
        var item = new DocumentListItemDto
        {
            Id = Guid.Parse(row.Id),
            Name = row.Name,
            FolderId = string.IsNullOrEmpty(row.FolderId) ? null : Guid.Parse(row.FolderId),
            DossierId = string.IsNullOrEmpty(row.DossierId) ? null : Guid.Parse(row.DossierId),
            CreatedBy = row.CreatedBy,
            CreatedByName = row.CreatedByName,
            CreatedDate = row.CreatedDate,
            FileSize = row.FileSize,
            MimeType = row.MimeType,
            LatestVersionId = string.IsNullOrEmpty(row.LatestVersionId) ? null : Guid.Parse(row.LatestVersionId),
            DocumentTypeId = string.IsNullOrEmpty(row.DocumentTypeId) ? null : Guid.Parse(row.DocumentTypeId),
            DocumentTypeName = row.DocumentTypeName,
        };

        if (!string.IsNullOrEmpty(row.OcrProgressId))
        {
            item.OcrProgress = new DocumentOcrProgressSummaryDto
            {
                Id = Guid.Parse(row.OcrProgressId),
                DocumentVersionId = Guid.Parse(row.OcrDocumentVersionId!),
                Phase = row.OcrPhase ?? "ocr",
                CurrentPage = row.OcrCurrentPage,
                TotalPages = row.OcrTotalPages,
                Progress = row.OcrProgress,
                Status = row.OcrStatus ?? string.Empty,
                ProcessOption = row.OcrProcessOption
            };
        }

        if (!string.IsNullOrEmpty(row.ExtractionResultId))
        {
            item.ExtractionResult = new DocumentExtractionResultSummaryDto
            {
                Id = Guid.Parse(row.ExtractionResultId),
                DocumentVersionId = Guid.Parse(row.ExtractionDocumentVersionId!),
                Status = row.ExtractionStatus ?? string.Empty
            };
        }

        return item;
    }

    private sealed class DossierDocumentListRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FolderId { get; set; }
        public string? DossierId { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public long FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? LatestVersionId { get; set; }
        public string? DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
        public string? OcrProgressId { get; set; }
        public string? OcrDocumentVersionId { get; set; }
        public string? OcrPhase { get; set; }
        public int OcrCurrentPage { get; set; }
        public int OcrTotalPages { get; set; }
        public int OcrProgress { get; set; }
        public string? OcrStatus { get; set; }
        public string? OcrProcessOption { get; set; }
        public string? ExtractionResultId { get; set; }
        public string? ExtractionDocumentVersionId { get; set; }
        public string? ExtractionStatus { get; set; }
    }

    public async Task<bool> AssignDocumentToDossierAsync(Guid documentId, Guid dossierId, Guid documentTypeId, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            UPDATE DOCUMENTS
            SET FOLDER_ID = NULL,
                DOSSIER_ID = :DossierId,
                DOCUMENT_TYPE_ID = :DocumentTypeId,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP,
                ROW_VERSION = ROW_VERSION + 1
            WHERE ID = :Id
              AND IS_DELETED = 0
              AND FOLDER_ID IS NOT NULL
              AND DOSSIER_ID IS NULL";

        var rows = await _connection.ExecuteAsync(sql, new
        {
            Id = documentId.ToString(),
            DossierId = dossierId.ToString(),
            DocumentTypeId = documentTypeId.ToString(),
            ModifiedBy = modifiedBy
        });
        return rows > 0;
    }

    public async Task<bool> UpdateDocumentVersionFilePathAsync(Guid versionId, string filePath, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            UPDATE DOCUMENT_VERSIONS
            SET FILE_PATH = :FilePath
            WHERE ID = :Id AND IS_DELETED = 0";

        var rows = await _connection.ExecuteAsync(sql, new
        {
            Id = versionId.ToString(),
            FilePath = filePath
        });
        return rows > 0;
    }

    public async Task<bool> SoftDeleteDocumentVersionsAsync(Guid documentId, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            UPDATE DOCUMENT_VERSIONS
            SET IS_DELETED = 1
            WHERE DOCUMENT_ID = :DocumentId AND IS_DELETED = 0";

        var rows = await _connection.ExecuteAsync(sql, new
        {
            DocumentId = documentId.ToString()
        });
        return rows > 0;
    }

    public async Task<string?> GetOrganizationUnitCodeAsync(long unitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = "SELECT CODE FROM ORGANIZATION_UNIT WHERE ID = :UnitId";
        return await _connection.QuerySingleOrDefaultAsync<string?>(sql, new { UnitId = unitId });
    }

    public async Task<bool> DocumentBelongsToDossierAsync(Guid documentId, Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT COUNT(1) FROM DOCUMENTS
            WHERE ID = :Id AND DOSSIER_ID = :DossierId AND IS_DELETED = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new
        {
            Id = documentId.ToString(),
            DossierId = dossierId.ToString()
        });
        return count > 0;
    }

    public async Task<bool> VersionBelongsToDossierAsync(Guid versionId, Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT COUNT(1)
            FROM DOCUMENT_VERSIONS dv
            INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID
            WHERE dv.ID = :VersionId
              AND d.DOSSIER_ID = :DossierId
              AND dv.IS_DELETED = 0
              AND d.IS_DELETED = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new
        {
            VersionId = versionId.ToString(),
            DossierId = dossierId.ToString()
        });
        return count > 0;
    }

    public async Task<Guid?> GetDossierIdByVersionIdAsync(Guid versionId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT d.DOSSIER_ID
            FROM DOCUMENT_VERSIONS dv
            INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID
            WHERE dv.ID = :VersionId
              AND dv.IS_DELETED = 0
              AND d.IS_DELETED = 0";

        var dossierId = await _connection.QuerySingleOrDefaultAsync<string?>(sql, new
        {
            VersionId = versionId.ToString()
        });

        return string.IsNullOrWhiteSpace(dossierId) ? null : Guid.Parse(dossierId);
    }

    public async Task<UnitQueryDto?> GetUnitInfoAsync(long unitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string unitSql = "SELECT Name, Code FROM ORGANIZATION_UNIT WHERE Id = :UnitId";
        return await _connection.QuerySingleOrDefaultAsync<UnitQueryDto>(unitSql, new { UnitId = unitId });
    }

    public async Task<IEnumerable<DossierTypeQueryDto>> GetActiveDossierTypesWithGridTypeAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string dossierTypeSql = @"
            SELECT dt.ID as Id, dt.NAME as Name, dt.CODE as Code, f.GridTypeId
            FROM DOSSIER_TYPES dt
            LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
            WHERE dt.IsDeleted = 0 AND dt.IS_ACTIVE = 1
            ORDER BY dt.PIORITY ASC, dt.NAME ASC";

        return await _connection.QueryAsync<DossierTypeQueryDto>(dossierTypeSql);
    }

    public async Task<IEnumerable<InfrastructureQueryDto>> GetActiveInfrastructuresByUnitAsync(long unitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string infrastructureSql = @"
            SELECT ID as Id, NAME as Name, CODE as Code, INFRA_TYPE_ID as InfraTypeId
            FROM INFRASTRUCTURE
            WHERE IsDeleted = 0 AND IS_ACTIVE = 1 AND INFRA_TYPE_ID IN (1, 2) 
              AND UNIT_ID IN (
                  SELECT Id 
                  FROM ORGANIZATION_UNIT
                  START WITH Id = :UnitId
                  CONNECT BY PRIOR Id = ParentId
              )
            ORDER BY NAME ASC";

        return await _connection.QueryAsync<InfrastructureQueryDto>(infrastructureSql, new { UnitId = unitId });
    }

    public async Task<IEnumerable<ActiveDossierQueryDto>> GetActiveDossiersByUnitAsync(long unitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT 
                d.ID as Id, 
                d.InfrastructureId as InfrastructureId, 
                d.DossierTypeId as DossierTypeId, 
                d.DossierSetId as DossierSetId, 
                d.FormDataJson as FormDataJson,
                dt.NAME as DossierTypeName, 
                ds.NAME as DossierSetName,
                f.GridTypeId as GridTypeId
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOSSIER_SETS ds ON d.DossierSetId = ds.ID
            LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
            WHERE d.ISDELETED = 0 
              AND d.PublishStatusId = 2
              AND i.UNIT_ID IN (
                  SELECT Id 
                  FROM ORGANIZATION_UNIT
                  START WITH Id = :UnitId
                  CONNECT BY PRIOR Id = ParentId
              )
              AND i.IsDeleted = 0 
              AND i.IS_ACTIVE = 1";

        return await _connection.QueryAsync<ActiveDossierQueryDto>(sql, new { UnitId = unitId });
    }


    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDossierCatalogDocumentsAsync(
        long unitId, 
        string? infrastructureId, 
        string? dossierTypeId, 
        string? keyword, 
        int page, 
        int pageSize)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var parameters = new DynamicParameters();
        parameters.Add("UnitId", unitId);
        parameters.Add("InfrastructureId", infrastructureId);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var whereClause = "dos.IsDeleted = 0 AND d.IS_DELETED = 0 AND i.UNIT_ID = :UnitId AND dos.InfrastructureId = :InfrastructureId";

        if (!string.IsNullOrWhiteSpace(dossierTypeId))
        {
            whereClause += " AND dos.DossierTypeId = :DossierTypeId";
            parameters.Add("DossierTypeId", dossierTypeId);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            whereClause += " AND LOWER(d.NAME) LIKE :Keyword";
            parameters.Add("Keyword", $"%{keyword.ToLower().Trim()}%");
        }

        var countSql = $@"
            SELECT COUNT(1)
            FROM DOCUMENTS d
            JOIN DOSSIERS dos ON d.DOSSIER_ID = dos.ID
            JOIN INFRASTRUCTURE i ON dos.InfrastructureId = i.ID
            WHERE {whereClause}";

        var countResult = await _connection.ExecuteScalarAsync(countSql, parameters);
        var totalCount = countResult != null && countResult != DBNull.Value ? Convert.ToInt32(countResult) : 0;

        if (totalCount == 0)
        {
            return (Enumerable.Empty<DocumentListItemDto>(), 0);
        }

        var listSql = $@"
            SELECT 
                d.ID,
                d.NAME,
                d.FOLDER_ID AS FolderId,
                d.DOSSIER_ID AS DossierId,
                d.CREATED_BY AS CreatedBy,
                {DocumentCreatedByNameSelect},
                d.CREATED_DATE AS CreatedDate,
                NVL(latest.FILE_SIZE, 0) AS FileSize,
                latest.MIME_TYPE AS MimeType,
                latest.LATEST_VERSION_ID AS LatestVersionId
            FROM DOCUMENTS d
            JOIN DOSSIERS dos ON d.DOSSIER_ID = dos.ID
            JOIN INFRASTRUCTURE i ON dos.InfrastructureId = i.ID
            {DocumentCreatorJoin}
            LEFT JOIN (
                SELECT dv.DOCUMENT_ID, dv.ID AS LATEST_VERSION_ID, dv.FILE_SIZE, dv.MIME_TYPE
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN (
                    SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                    FROM DOCUMENT_VERSIONS
                    WHERE IS_DELETED = 0
                    GROUP BY DOCUMENT_ID
                ) mx ON mx.DOCUMENT_ID = dv.DOCUMENT_ID AND mx.MAX_VER = dv.VERSION_NUMBER
                WHERE dv.IS_DELETED = 0
            ) latest ON latest.DOCUMENT_ID = d.ID
            WHERE {whereClause}
            ORDER BY d.CREATED_DATE DESC, d.NAME ASC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var items = await _connection.QueryAsync<DocumentListItemDto>(listSql, parameters);

        return (items, totalCount);
    }
}
