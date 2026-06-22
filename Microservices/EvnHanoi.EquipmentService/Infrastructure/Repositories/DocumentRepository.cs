using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly IDbConnection _connection;

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
                d.CREATED_DATE AS CreatedDate,
                NVL(latest.FILE_SIZE, 0) AS FileSize,
                latest.MIME_TYPE AS MimeType,
                latest.LATEST_VERSION_ID AS LatestVersionId
            FROM DOCUMENTS d
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
                d.CREATED_BY AS CreatedBy,
                d.CREATED_DATE AS CreatedDate,
                NVL(latest.FILE_SIZE, 0) AS FileSize,
                latest.MIME_TYPE AS MimeType,
                latest.LATEST_VERSION_ID AS LatestVersionId
            FROM DOCUMENTS d
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
                ROW_VERSION,
                CREATED_BY,
                CREATED_DATE,
                IS_DELETED
            ) VALUES (
                :Id,
                :Name,
                :FolderId,
                :DossierId,
                1,
                :CreatedBy,
                SYSTIMESTAMP,
                0
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            document.Name,
            FolderId = document.FolderId?.ToString(),
            DossierId = document.DossierId?.ToString(),
            document.CreatedBy
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
            FolderId = session.FolderId.ToString(),
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
}
