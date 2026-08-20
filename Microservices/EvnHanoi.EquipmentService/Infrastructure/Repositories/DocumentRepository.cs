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

    /// <summary>
    /// DOCUMENT_TYPES dùng cột IsDeleted (Oracle: ISDELETED), không phải IS_DELETED như bảng DOCUMENTS.
    /// </summary>
    private const string DocumentTypeActiveJoin =
        $"dt.ID = d.DOCUMENT_TYPE_ID AND dt.{nameof(DocumentType.IsDeleted)} = 0";

    private const string EavFormTemplateActiveFilter =
        $"f.{nameof(EavFormTemplate.IsDeleted)} = 0";

    public DocumentRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByDossierIdsAsync(
        IEnumerable<Guid> dossierIds,
        string? keyword,
        int page,
        int pageSize)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var idList = dossierIds.Select(x => x.ToString()).ToList();
        if (idList.Count == 0)
            return (Enumerable.Empty<DocumentListItemDto>(), 0);

        var parameters = new DynamicParameters();
        parameters.Add("DossierIds", idList.ToArray());

        var whereClause = "d.IS_DELETED = 0 AND d.DOSSIER_ID IN :DossierIds";
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            whereClause += " AND d.NAME LIKE :Keyword";
            parameters.Add("Keyword", $"%{keyword}%");
        }

        var countSql = $"SELECT COUNT(*) FROM DOCUMENTS d WHERE {whereClause}";
        var countResult = await _connection.ExecuteScalarAsync(countSql, parameters);
        var totalCount = countResult != null && countResult != DBNull.Value ? Convert.ToInt32(countResult) : 0;

        if (totalCount == 0)
            return (Enumerable.Empty<DocumentListItemDto>(), 0);

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

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
            LEFT JOIN DOCUMENT_TYPES dt ON {DocumentTypeActiveJoin}
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
            WHERE {whereClause}
            ORDER BY d.CREATED_DATE DESC, d.NAME ASC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rows = await _connection.QueryAsync<DossierDocumentListRow>(listSql, parameters);
        return (rows.Select(MapDossierDocumentListRow), totalCount);
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
                NVL(NULLIF(TRIM(fcu.FullName), ''), NULLIF(TRIM(f.CREATED_BY), '')) AS CreatedByName,
                f.CREATED_DATE AS CreatedDate,
                f.MODIFIED_BY AS ModifiedBy,
                f.MODIFIED_DATE AS ModifiedDate,
                f.ROW_VERSION AS RowVersion
            FROM FOLDERS f
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = f.UNIT_ID
            LEFT JOIN APP_USER fcu ON (
                LOWER(TRIM(fcu.Id)) = LOWER(TRIM(f.CREATED_BY))
                OR LOWER(TRIM(fcu.UserName)) = LOWER(TRIM(f.CREATED_BY))
            )
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
                NVL(NULLIF(TRIM(fcu.FullName), ''), NULLIF(TRIM(f.CREATED_BY), '')) AS CreatedByName,
                f.CREATED_DATE AS CreatedDate,
                f.MODIFIED_BY AS ModifiedBy,
                f.MODIFIED_DATE AS ModifiedDate,
                f.ROW_VERSION AS RowVersion
            FROM FOLDERS f
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = f.UNIT_ID
            LEFT JOIN APP_USER fcu ON (
                LOWER(TRIM(fcu.Id)) = LOWER(TRIM(f.CREATED_BY))
                OR LOWER(TRIM(fcu.UserName)) = LOWER(TRIM(f.CREATED_BY))
            )
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

    public async Task<IEnumerable<FolderNodeDto>> GetChildFoldersByParentAsync(Guid parentId)
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
                NVL(NULLIF(TRIM(fcu.FullName), ''), NULLIF(TRIM(f.CREATED_BY), '')) AS CreatedByName,
                f.CREATED_DATE AS CreatedDate,
                f.MODIFIED_BY AS ModifiedBy,
                f.MODIFIED_DATE AS ModifiedDate,
                f.ROW_VERSION AS RowVersion
            FROM FOLDERS f
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = f.UNIT_ID
            LEFT JOIN APP_USER fcu ON (
                LOWER(TRIM(fcu.Id)) = LOWER(TRIM(f.CREATED_BY))
                OR LOWER(TRIM(fcu.UserName)) = LOWER(TRIM(f.CREATED_BY))
            )
            WHERE f.PARENT_ID = :ParentId
              AND f.IS_DELETED = 0
            ORDER BY f.NAME ASC";

        return await _connection.QueryAsync<FolderNodeDto>(sql, new { ParentId = parentId.ToString() });
    }

    public async Task<IEnumerable<FolderZipDocumentDto>> GetFolderDocumentsForZipAsync(Guid folderId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT
                d.NAME AS DocumentName,
                dv.FILE_PATH AS FilePath
            FROM DOCUMENTS d
            INNER JOIN (
                SELECT dv2.DOCUMENT_ID, dv2.FILE_PATH
                FROM DOCUMENT_VERSIONS dv2
                INNER JOIN (
                    SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                    FROM DOCUMENT_VERSIONS
                    WHERE IS_DELETED = 0
                    GROUP BY DOCUMENT_ID
                ) mx ON mx.DOCUMENT_ID = dv2.DOCUMENT_ID AND mx.MAX_VER = dv2.VERSION_NUMBER
                WHERE dv2.IS_DELETED = 0
                  AND dv2.FILE_PATH IS NOT NULL
            ) dv ON dv.DOCUMENT_ID = d.ID
            WHERE d.IS_DELETED = 0
              AND d.DOSSIER_ID IS NULL
              AND d.FOLDER_ID = :FolderId
            ORDER BY d.NAME ASC";

        return await _connection.QueryAsync<FolderZipDocumentDto>(sql, new { FolderId = folderId.ToString() });
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
            whereClause += " AND LOWER(NAME) LIKE :Keyword";
            aliasedWhereClause += " AND LOWER(d.NAME) LIKE :Keyword";
        }

        if (!string.IsNullOrWhiteSpace(filter.CreatedBy))
        {
            whereClause += " AND (LOWER(CREATED_BY) LIKE :CreatedBy OR LOWER(CREATOR_NAME) LIKE :CreatedBy)";
            aliasedWhereClause += " AND (LOWER(d.CREATED_BY) LIKE :CreatedBy OR LOWER(d.CREATOR_NAME) LIKE :CreatedBy OR LOWER(cu.FullName) LIKE :CreatedBy)";
        }

        if (filter.StartDate.HasValue)
        {
            whereClause += " AND CREATED_DATE >= :StartDate";
            aliasedWhereClause += " AND d.CREATED_DATE >= :StartDate";
        }

        if (filter.EndDate.HasValue)
        {
            whereClause += " AND CREATED_DATE <= :EndDate";
            aliasedWhereClause += " AND d.CREATED_DATE <= :EndDate";
        }

        // Count total
        var countSql = $"SELECT COUNT(*) FROM DOCUMENTS WHERE {whereClause}";
        var keywordParam = !string.IsNullOrWhiteSpace(filter.Keyword) ? $"%{filter.Keyword.Trim().ToLower()}%" : null;
        var creatorParam = !string.IsNullOrWhiteSpace(filter.CreatedBy) ? $"%{filter.CreatedBy.Trim().ToLower()}%" : null;
        var endOfDayVal = filter.EndDate.HasValue ? filter.EndDate.Value.Date.AddDays(1).AddTicks(-1) : (DateTime?)null;

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            countSql,
            new
            {
                FolderId = folderId?.ToString(),
                Keyword = keywordParam,
                CreatedBy = creatorParam,
                StartDate = filter.StartDate,
                EndDate = endOfDayVal
            }
        );

        // Get paged data (join phiên bản mới nhất để lấy FILE_SIZE, MIME_TYPE)
        var offset = (filter.Page - 1) * filter.PageSize;

        // Build sorting clause dynamically and safely
        var sortField = "d.CREATED_DATE";
        if (!string.IsNullOrEmpty(filter.SortField))
        {
            sortField = filter.SortField.ToLowerInvariant() switch
            {
                "name" => "d.NAME",
                "filesize" => "NVL(latest.FILE_SIZE, 0)",
                "createddate" => "d.CREATED_DATE",
                "createdby" => "d.CREATED_BY",
                _ => "d.CREATED_DATE"
            };
        }

        var sortOrder = "DESC";
        if (!string.IsNullOrEmpty(filter.SortOrder) && filter.SortOrder.ToLowerInvariant() == "asc")
        {
            sortOrder = "ASC";
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
                latest.LATEST_VERSION_ID AS LatestVersionId,
                d.ROW_VERSION AS RowVersion
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
            ORDER BY {sortField} {sortOrder}, d.NAME ASC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var items = await _connection.QueryAsync<DocumentListItemDto>(
            listSql,
            new
            {
                FolderId = folderId?.ToString(),
                Keyword = keywordParam,
                CreatedBy = creatorParam,
                StartDate = filter.StartDate,
                EndDate = endOfDayVal,
                Offset = offset,
                PageSize = filter.PageSize
            }
        );

        return (items, totalCount);
    }

    public async Task<DocumentListItemDto?> GetDocumentByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"
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
            LEFT JOIN DOCUMENT_TYPES dt ON {DocumentTypeActiveJoin}
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

        try
        {
            return await _connection.QuerySingleOrDefaultAsync<DocumentListItemDto>(sql, new { Id = id.ToString() });
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(@"C:\Users\admin\Desktop\SO_HOA_X10\oracle_error.log", ex.ToString() + "\n\nSQL:\n" + sql);
            }
            catch { }
            throw;
        }
    }

    public async Task<Document?> GetDocumentByNameAndFolderAsync(string name, Guid folderId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT 
                ID,
                NAME,
                FOLDER_ID AS FolderId,
                DOSSIER_ID AS DossierId,
                DOCUMENT_TYPE_ID AS DocumentTypeId,
                STATUS,
                ROW_VERSION AS RowVersion,
                CREATED_BY AS CreatedBy,
                CREATOR_NAME AS CreatorName,
                CREATED_DATE AS CreatedDate,
                MODIFIED_BY AS ModifiedBy,
                MODIFIED_DATE AS ModifiedDate,
                IS_DELETED AS IsDeleted
            FROM DOCUMENTS
            WHERE NAME = :Name 
              AND FOLDER_ID = :FolderId 
              AND IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<Document>(sql, new
        {
            Name = name,
            FolderId = folderId.ToString()
        });
    }

    // New method: Get document by name within a dossier
    public async Task<Document?> GetDocumentByNameAndDossierAsync(string name, Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT 
                ID,
                NAME,
                FOLDER_ID AS FolderId,
                DOSSIER_ID AS DossierId,
                DOCUMENT_TYPE_ID AS DocumentTypeId,
                STATUS,
                ROW_VERSION AS RowVersion,
                CREATED_BY AS CreatedBy,
                CREATOR_NAME AS CreatorName,
                CREATED_DATE AS CreatedDate,
                MODIFIED_BY AS ModifiedBy,
                MODIFIED_DATE AS ModifiedDate,
                IS_DELETED AS IsDeleted
            FROM DOCUMENTS
            WHERE NAME = :Name 
              AND DOSSIER_ID = :DossierId 
              AND IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<Document>(sql, new
        {
            Name = name,
            DossierId = dossierId.ToString()
        });
    }

    // New method: Get the highest version number for a document
    public async Task<int> GetMaxDocumentVersionNumberAsync(Guid documentId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT NVL(MAX(VERSION_NUMBER), 0)
            FROM DOCUMENT_VERSIONS
            WHERE DOCUMENT_ID = :DocumentId
              AND IS_DELETED = 0";

        var result = await _connection.ExecuteScalarAsync<int>(sql, new { DocumentId = documentId.ToString() });
        return result;
    }


    public async Task<EavFormTemplate?> GetEavFormTemplateByDocumentIdAsync(Guid documentId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"
            SELECT
                f.Id, v.Name as Name, f.Code as Code, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo, f.ExtractionProcess,
                v.FormSchema as FormSchema, f.EquipmentTypeId, f.GridTypeId, v.Version as Version, f.IsActive as IsActive, f.CreatedAt,
                f.CreatedBy, f.Status, f.FormType, f.IsDeleted
            FROM DOCUMENTS d
            INNER JOIN DOCUMENT_TYPES dt ON {DocumentTypeActiveJoin}
            INNER JOIN EavFormTemplates f ON f.Id = dt.FORM_ID AND {EavFormTemplateActiveFilter}
            LEFT JOIN EavFormTemplateVersions v ON f.Id = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = f.Id AND IsActive = 1 AND IsDeleted = 0
            )
            WHERE d.ID = :DocumentId AND d.IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(
            sql,
            new { DocumentId = documentId.ToString() });
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
                MINIO_VERSION_ID,
                FILE_SIZE,
                MIME_TYPE,
                PAGE_COUNT,
                CREATED_BY,
                CREATED_DATE,
                IS_DELETED
            ) VALUES (
                :Id,
                :DocumentId,
                :VersionNumber,
                :UploadSource,
                :FilePath,
                :MinioVersionId,
                :FileSize,
                :MimeType,
                :PageCount,
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
            version.MinioVersionId,
            version.FileSize,
            version.MimeType,
            version.PageCount,
            version.CreatedBy
        });

        return id;
    }

    public async Task<Guid> CreateDocumentSignHistoryAsync(DocumentSignHistory history)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var id = Guid.NewGuid();
        history.Id = id;

        var sql = @"
            INSERT INTO DOCUMENT_SIGN_HISTORY (
                Id,
                DocumentId,
                DocumentVersionId,
                SignerUserId,
                SignerName,
                SerialNumber,
                SignedAt,
                Status,
                ErrorMessage,
                CreatedBy,
                CreatedDate,
                IsDeleted
            ) VALUES (
                :Id,
                :DocumentId,
                :DocumentVersionId,
                :SignerUserId,
                :SignerName,
                :SerialNumber,
                :SignedAt,
                :Status,
                :ErrorMessage,
                :CreatedBy,
                SYSTIMESTAMP,
                0
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            DocumentId = history.DocumentId.ToString(),
            DocumentVersionId = history.DocumentVersionId?.ToString(),
            history.SignerUserId,
            history.SignerName,
            history.SerialNumber,
            history.SignedAt,
            history.Status,
            history.ErrorMessage,
            history.CreatedBy
        });

        return id;
    }

    public async Task<IEnumerable<DocumentVersionDto>> GetDocumentVersionsAsync(Guid documentId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                dv.ID,
                dv.DOCUMENT_ID AS DocumentId,
                dv.VERSION_NUMBER AS VersionNumber,
                dv.UPLOAD_SOURCE AS UploadSource,
                dv.FILE_PATH AS FilePath,
                dv.MINIO_VERSION_ID AS MinioVersionId,
                dv.FILE_SIZE AS FileSize,
                dv.MIME_TYPE AS MimeType,
                dv.CREATED_BY AS CreatedBy,
                NVL(cu.FullName, dv.CREATED_BY) AS CreatedByName,
                dv.CREATED_DATE AS CreatedDate
            FROM DOCUMENT_VERSIONS dv
            LEFT JOIN APP_USER cu ON (
                LOWER(cu.Id) = LOWER(dv.CREATED_BY)
                OR LOWER(cu.UserName) = LOWER(dv.CREATED_BY)
            )
            WHERE dv.DOCUMENT_ID = :DocumentId 
              AND dv.IS_DELETED = 0
            ORDER BY dv.VERSION_NUMBER DESC";

        return await _connection.QueryAsync<DocumentVersionDto>(sql, new { DocumentId = documentId.ToString() });
    }

    public async Task<DocumentVersionDto?> GetDocumentVersionByIdAsync(Guid versionId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                dv.ID,
                dv.DOCUMENT_ID AS DocumentId,
                dv.VERSION_NUMBER AS VersionNumber,
                dv.UPLOAD_SOURCE AS UploadSource,
                dv.FILE_PATH AS FilePath,
                dv.MINIO_VERSION_ID AS MinioVersionId,
                dv.FILE_SIZE AS FileSize,
                dv.MIME_TYPE AS MimeType,
                dv.CREATED_BY AS CreatedBy,
                NVL(cu.FullName, dv.CREATED_BY) AS CreatedByName,
                dv.CREATED_DATE AS CreatedDate
            FROM DOCUMENT_VERSIONS dv
            LEFT JOIN APP_USER cu ON (
                LOWER(cu.Id) = LOWER(dv.CREATED_BY)
                OR LOWER(cu.UserName) = LOWER(dv.CREATED_BY)
            )
            WHERE dv.ID = :Id 
              AND dv.IS_DELETED = 0";

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

        var countParams = new DynamicParameters();
        countParams.Add("DossierId", dossierId.ToString());
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            countParams.Add("Keyword", $"%{filter.Keyword}%");

        var countSql = $"SELECT COUNT(*) FROM DOCUMENTS d WHERE {aliasedWhere}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, countParams);

        var offset = (filter.Page - 1) * filter.PageSize;
        var listSql = $@"
            SELECT 
                d.ID,
                d.NAME,
                d.FOLDER_ID AS FolderId,
                d.DOSSIER_ID AS DossierId,
                equipment.EquipmentName AS EquipmentName,
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
            LEFT JOIN DOCUMENT_TYPES dt ON {DocumentTypeActiveJoin}
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
                SELECT de.DossierId,
                       LISTAGG(e.NAME, ', ') WITHIN GROUP (ORDER BY e.NAME) AS EquipmentName
                FROM DOSSIER_EQUIPMENTS de
                INNER JOIN EQUIPMENTS e ON e.ID = de.EquipmentId
                    AND e.ISDELETED = 0
                GROUP BY de.DossierId
            ) equipment ON equipment.DossierId = d.DOSSIER_ID
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

        var listParams = new DynamicParameters();
        listParams.Add("DossierId", dossierId.ToString());
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            listParams.Add("Keyword", $"%{filter.Keyword}%");
        listParams.Add("Offset", offset);
        listParams.Add("PageSize", filter.PageSize);

        try
        {
            var rows = await _connection.QueryAsync<DossierDocumentListRow>(listSql, listParams);
            var items = rows.Select(MapDossierDocumentListRow);
            return (items, totalCount);
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(@"C:\Users\admin\Desktop\SO_HOA_X10\oracle_error.log", ex.ToString() + "\n\nSQL:\n" + listSql);
            }
            catch { }
            throw;
        }
    }

    private static DocumentListItemDto MapDossierDocumentListRow(DossierDocumentListRow row)
    {
        var item = new DocumentListItemDto
        {
            Id = Guid.Parse(row.Id),
            Name = row.Name,
            FolderId = string.IsNullOrEmpty(row.FolderId) ? null : Guid.Parse(row.FolderId),
            DossierId = string.IsNullOrEmpty(row.DossierId) ? null : Guid.Parse(row.DossierId),
            EquipmentName = row.EquipmentName,
            CreatedBy = row.CreatedBy,
            CreatedByName = row.CreatedByName,
            CreatedDate = row.CreatedDate,
            FileSize = row.FileSize,
            MimeType = row.MimeType,
            LatestVersionId = string.IsNullOrEmpty(row.LatestVersionId) ? null : Guid.Parse(row.LatestVersionId),
            DocumentTypeId = string.IsNullOrEmpty(row.DocumentTypeId) ? null : Guid.Parse(row.DocumentTypeId),
            DocumentTypeName = row.DocumentTypeName,
            IsEquipmentProfile = row.IsEquipmentProfile == 1,
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
        public string? EquipmentName { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public long FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? LatestVersionId { get; set; }
        public string? DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
        public int IsEquipmentProfile { get; set; }
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

    public async Task<bool> SoftDeleteDocumentVersionAsync(Guid versionId, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            UPDATE DOCUMENT_VERSIONS
            SET IS_DELETED = 1
            WHERE ID = :VersionId AND IS_DELETED = 0";

        var rows = await _connection.ExecuteAsync(sql, new
        {
            VersionId = versionId.ToString()
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

    public async Task<bool> IsEquipmentProfileDocumentVersionForEquipmentAsync(Guid equipmentId, Guid versionId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = $@"
            SELECT COUNT(1)
            FROM DOCUMENT_VERSIONS dv
            INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID AND d.IS_DELETED = 0
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.DOSSIER_ID = de.DossierId AND de.EquipmentId = :EquipmentId
            WHERE dv.ID = :VersionId AND dv.IS_DELETED = 0";

        var count = await _connection.ExecuteScalarAsync<int>(sql, new
        {
            EquipmentId = equipmentId.ToString(),
            VersionId = versionId.ToString()
        });
        return count > 0;
    }

    public async Task<bool> IsPublishedEquipmentProfileDocumentVersionForEquipmentAsync(Guid equipmentId, Guid versionId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = $@"
            SELECT COUNT(1)
            FROM DOCUMENT_VERSIONS dv
            INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID AND d.IS_DELETED = 0
            INNER JOIN DOSSIERS dossier ON d.DOSSIER_ID = dossier.ID
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.DOSSIER_ID = de.DossierId AND de.EquipmentId = :EquipmentId
            WHERE dv.ID = :VersionId
              AND dv.IS_DELETED = 0
              AND dossier.IsDeleted = 0
              AND dossier.STATUS_ID = 6
              AND dossier.PUBLISHSTATUSID = 2";

        var count = await _connection.ExecuteScalarAsync<int>(sql, new
        {
            EquipmentId = equipmentId.ToString(),
            VersionId = versionId.ToString()
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

    public async Task<int?> GetDossierPublishStatusIdByVersionIdAsync(Guid versionId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT dos.PUBLISHSTATUSID
            FROM DOCUMENT_VERSIONS dv
            INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID AND d.IS_DELETED = 0
            INNER JOIN DOSSIERS dos ON dos.Id = d.DOSSIER_ID AND dos.IsDeleted = 0
            WHERE dv.ID = :VersionId
              AND dv.IS_DELETED = 0";

        var value = await _connection.QuerySingleOrDefaultAsync<int?>(sql, new
        {
            VersionId = versionId.ToString()
        });

        return value is null or 0 ? null : value;
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
            SELECT ID as Id, NAME as Name, CODE as Code, INFRA_TYPE_ID as InfraTypeId, GridTypeId
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

        // Một hồ sơ thuộc một đơn vị khi nó được gắn với một hạ tầng (lưới điện/trạm)
        // thuộc cây đơn vị qua FK trực tiếp (d.InfrastructureId) HOẶC qua bảng trung gian
        // DOSSIER_INFRASTRUCTURE (liên kết n-n). Giữ đúng logic "OR EXISTS" giống
        // GetListDocumentIdsAsync để số lượng tài liệu trên cây khớp đúng thực tế.
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
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOSSIER_SETS ds ON d.DossierSetId = ds.ID
            LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
            WHERE d.ISDELETED = 0 
              AND d.PublishStatusId = 2
              AND (
                    EXISTS (
                        SELECT 1
                        FROM INFRASTRUCTURE i
                        WHERE i.ID = d.InfrastructureId
                          AND i.UNIT_ID IN (
                              SELECT Id 
                              FROM ORGANIZATION_UNIT
                              START WITH Id = :UnitId
                              CONNECT BY PRIOR Id = ParentId
                          )
                          AND i.IsDeleted = 0 
                          AND i.IS_ACTIVE = 1
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM DOSSIER_INFRASTRUCTURE di
                        INNER JOIN INFRASTRUCTURE i2 ON i2.ID = di.InfrastructureId
                        WHERE di.DossierId = d.ID
                          AND i2.UNIT_ID IN (
                              SELECT Id 
                              FROM ORGANIZATION_UNIT
                              START WITH Id = :UnitId
                              CONNECT BY PRIOR Id = ParentId
                          )
                          AND i2.IsDeleted = 0 
                          AND i2.IS_ACTIVE = 1
                    )
              )";

        return await _connection.QueryAsync<ActiveDossierQueryDto>(sql, new { UnitId = unitId });
    }

    public async Task<Dictionary<string, int>> GetDocumentCountsByDossierIdsAsync(IEnumerable<string> dossierIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT DOSSIER_ID, COUNT(*) AS CNT
            FROM DOCUMENTS
            WHERE IS_DELETED = 0 AND DOSSIER_ID IN :DossierIds
            GROUP BY DOSSIER_ID";

        var results = await _connection.QueryAsync<(string DossierId, int Count)>(sql, new { DossierIds = dossierIds });
        return results.ToDictionary(r => r.DossierId, r => r.Count);
    }

    public async Task<IEnumerable<(string DossierId, string InfrastructureId)>> GetDossierInfrastructureLinksAsync(long unitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT di.DossierId, di.InfrastructureId
            FROM DOSSIER_INFRASTRUCTURE di
            INNER JOIN DOSSIERS d ON d.ID = di.DossierId AND d.ISDELETED = 0 AND d.PUBLISHSTATUSID = 2
            INNER JOIN INFRASTRUCTURE i ON i.ID = di.InfrastructureId
            WHERE i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT
                START WITH Id = :UnitId
                CONNECT BY PRIOR Id = ParentId
            )";

        var results = await _connection.QueryAsync<(string DossierId, string InfrastructureId)>(sql, new { UnitId = unitId });
        return results;
    }

    public async Task<DossierCatalogTreeDataDto> GetDossierCatalogTreeDataAsync(long unitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var result = new DossierCatalogTreeDataDto
        {
            UnitInfo = await GetUnitInfoAsync(unitId),
            Infrastructures = (await GetActiveInfrastructuresByUnitAsync(unitId)).ToList(),
            Dossiers = (await GetActiveDossiersByUnitAsync(unitId)).ToList()
        };

        result.JunctionLinks = (await GetDossierInfrastructureLinksAsync(unitId))
            .Select(x => new DossierInfrastructureLinkDto
            {
                DossierId = x.DossierId,
                InfrastructureId = x.InfrastructureId
            })
            .ToList();

        var dossierIds = result.Dossiers.Select(d => d.Id).ToList();
        if (dossierIds.Count > 0)
        {
            result.DocumentCounts = await GetDocumentCountsByDossierIdsAsync(dossierIds);
        }

        return result;
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

        // Chỉ đếm tài liệu của hồ sơ thuộc hạ tầng được chọn qua FK trực tiếp HOẶC bảng
        // trung gian DOSSIER_INFRASTRUCTURE (giống GetListDocumentIdsAsync), đồng thời
        // hồ sơ phải đã xuất bản và hạ tầng phải thuộc cây đơn vị, đang hoạt động.
        var whereClause = @"dos.IsDeleted = 0 AND dos.PublishStatusId = 2 AND d.IS_DELETED = 0
    AND (
        dos.InfrastructureId = :InfrastructureId
        OR EXISTS (
            SELECT 1 FROM DOSSIER_INFRASTRUCTURE di
            WHERE di.DossierId = dos.ID AND di.InfrastructureId = :InfrastructureId
        )
    )
    AND EXISTS (
        SELECT 1 FROM INFRASTRUCTURE ia
        WHERE ia.ID = :InfrastructureId
          AND ia.UNIT_ID IN (
              SELECT Id FROM ORGANIZATION_UNIT
              START WITH Id = :UnitId
              CONNECT BY PRIOR Id = ParentId
          )
          AND ia.IsDeleted = 0 AND ia.IS_ACTIVE = 1
    )";

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

    public async Task<IEnumerable<DocumentOcrIndexHintDto>> GetOcrVersionIndexHintsByDossierIdAsync(Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        // Hồ sơ đã xuất bản không đổi version — lấy phiên bản mới nhất đã OCR mỗi tài liệu.
        const string sql = @"
            SELECT
                dv.ID AS VersionId,
                NVL(ocr.BUCKET_NAME, 'dossiers') AS BucketName,
                NVL(ocr.FILE_PATH, dv.FILE_PATH) AS FilePath,
                NVL(ocr.TOTAL_PAGES, 0) AS TotalPages
            FROM DOCUMENTS d
            INNER JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = d.ID AND dv.IS_DELETED = 0
            INNER JOIN (
                SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                FROM DOCUMENT_VERSIONS
                WHERE IS_DELETED = 0
                GROUP BY DOCUMENT_ID
            ) mx ON mx.DOCUMENT_ID = dv.DOCUMENT_ID AND mx.MAX_VER = dv.VERSION_NUMBER
            INNER JOIN (
                SELECT DOCUMENT_VERSION_ID, BUCKET_NAME, FILE_PATH, TOTAL_PAGES
                FROM (
                    SELECT DOCUMENT_VERSION_ID, BUCKET_NAME, FILE_PATH, TOTAL_PAGES,
                           ROW_NUMBER() OVER (
                               PARTITION BY DOCUMENT_VERSION_ID
                               ORDER BY CREATED_DATE DESC) AS RN
                    FROM DOCUMENT_OCR_PROGRESS
                    WHERE IS_DELETED = 0
                      AND STATUS IN ('OcrCompleted', 'Completed', 'Extracting')
                )
                WHERE RN = 1
            ) ocr ON ocr.DOCUMENT_VERSION_ID = dv.ID
            WHERE d.DOSSIER_ID = :DossierId
              AND d.IS_DELETED = 0";

        var rows = await _connection.QueryAsync<dynamic>(sql, new { DossierId = dossierId.ToString() });
        return rows.Select(r => new DocumentOcrIndexHintDto
        {
            VersionId = r.VERSIONID is string sId && Guid.TryParse(sId, out var gId) ? gId : (r.VERSIONID is Guid guidId ? guidId : Guid.Empty),
            BucketName = r.BUCKETNAME,
            FilePath = r.FILEPATH ?? string.Empty,
            TotalPages = Convert.ToInt32(r.TOTALPAGES)
        })
        .Where(h => h.VersionId != Guid.Empty)
        .ToList();
    }

    public async Task<IEnumerable<Guid>> GetActiveVersionIdsByDossierIdAsync(Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT 
                dv.ID
            FROM DOCUMENTS d
            INNER JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = d.ID AND dv.IS_DELETED = 0
            INNER JOIN (
                SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                FROM DOCUMENT_VERSIONS
                WHERE IS_DELETED = 0
                GROUP BY DOCUMENT_ID
            ) mx ON mx.DOCUMENT_ID = dv.DOCUMENT_ID AND mx.MAX_VER = dv.VERSION_NUMBER
            WHERE d.DOSSIER_ID = :DossierId
              AND d.IS_DELETED = 0";

        var rows = await _connection.QueryAsync<string>(sql, new { DossierId = dossierId.ToString() });
        return rows.Select(Guid.Parse).ToList();
    }

    public Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetProfileDocumentsByEquipmentAsync(
        Guid equipmentId,
        DossierDocumentFilterDto filter) =>
        GetProfileDocumentsByEquipmentAsync(equipmentId, filter, publishedOnly: false);

    public Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetPublishedProfileDocumentsByEquipmentAsync(
        Guid equipmentId,
        DossierDocumentFilterDto filter) =>
        GetProfileDocumentsByEquipmentAsync(equipmentId, filter, publishedOnly: true);

    public Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetPublishedFactoryAcceptanceDocumentsByEquipmentAsync(
        Guid equipmentId,
        DossierDocumentFilterDto filter) =>
        GetProfileDocumentsByEquipmentAsync(equipmentId, filter, publishedOnly: true, documentTypeFlagColumn: "IS_FACTORY_ACCEPTANCE_REPORT");

    public Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetPublishedCbmDocumentsByEquipmentAsync(
        Guid equipmentId,
        DossierDocumentFilterDto filter) =>
        GetProfileDocumentsByEquipmentAsync(equipmentId, filter, publishedOnly: true, documentTypeFlagColumn: "IS_CBM_DOCUMENT");

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetPublishedFactoryAcceptanceDocumentsAsync(
        DossierDocumentFilterDto filter)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var whereClause = @"d.IS_DELETED = 0
                            AND dossier.IsDeleted = 0
                            AND dossier.STATUS_ID = 6
                            AND dossier.PUBLISHSTATUSID = 2
                            AND e.IsDeleted = 0
                            AND dt.IS_FACTORY_ACCEPTANCE_REPORT = 1";
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            whereClause += " AND d.NAME LIKE :Keyword";
            parameters.Add("Keyword", $"%{filter.Keyword.Trim()}%");
        }

        var fromClause = $@"FROM DOCUMENTS d
                            INNER JOIN DOSSIERS dossier ON d.DOSSIER_ID = dossier.ID
                            INNER JOIN DOSSIER_EQUIPMENTS de ON dossier.ID = de.DossierId
                            INNER JOIN EQUIPMENTS e ON de.EquipmentId = e.ID
                            INNER JOIN DOCUMENT_TYPES dt ON {DocumentTypeActiveJoin}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) {fromClause} WHERE {whereClause}",
            parameters);

        var listSql = $@"SELECT d.ID,
                                d.NAME,
                                d.FOLDER_ID AS FolderId,
                                d.DOSSIER_ID AS DossierId,
                                d.DOCUMENT_TYPE_ID AS DocumentTypeId,
                                dt.NAME AS DocumentTypeName,
                                dt.IS_EQUIPMENT_PROFILE AS IsEquipmentProfile,
                                e.NAME AS EquipmentName,
                                d.CREATED_BY AS CreatedBy,
                                {DocumentCreatedByNameSelect},
                                d.CREATED_DATE AS CreatedDate,
                                NVL(latest.FILE_SIZE, 0) AS FileSize,
                                latest.MIME_TYPE AS MimeType,
                                latest.LATEST_VERSION_ID AS LatestVersionId
                         {fromClause}
                         {DocumentCreatorJoin}
                         LEFT JOIN (
                             SELECT dv.DOCUMENT_ID, dv.ID AS LATEST_VERSION_ID, dv.FILE_SIZE, dv.MIME_TYPE
                             FROM DOCUMENT_VERSIONS dv
                             INNER JOIN (
                                 SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                                 FROM DOCUMENT_VERSIONS
                                 WHERE IS_DELETED = 0
                                 GROUP BY DOCUMENT_ID
                             ) latestVersion ON dv.DOCUMENT_ID = latestVersion.DOCUMENT_ID
                                              AND dv.VERSION_NUMBER = latestVersion.MAX_VER
                             WHERE dv.IS_DELETED = 0
                         ) latest ON latest.DOCUMENT_ID = d.ID
                         WHERE {whereClause}
                         ORDER BY d.CREATED_DATE DESC, d.NAME ASC
                         OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);
        parameters.Add("PageSize", filter.PageSize);
        var rows = await _connection.QueryAsync<DossierDocumentListRow>(listSql, parameters);
        return (rows.Select(MapDossierDocumentListRow), totalCount);
    }

    private async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetProfileDocumentsByEquipmentAsync(
        Guid equipmentId,
        DossierDocumentFilterDto filter,
        bool publishedOnly,
        string? documentTypeFlagColumn = null)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var dossierJoin = publishedOnly
            ? " INNER JOIN DOSSIERS dossier ON d.DOSSIER_ID = dossier.ID"
            : string.Empty;
        var publishedDossierFilter = publishedOnly
            ? " AND dossier.IsDeleted = 0 AND dossier.STATUS_ID = 6 AND dossier.PUBLISHSTATUSID = 2"
            : string.Empty;
        // documentTypeFlagColumn = null: tab "Tài liệu đính kèm" hiển thị mọi loại tài liệu, không lọc theo cờ loại tài liệu.
        var documentTypeFilter = documentTypeFlagColumn != null ? $" AND dt.{documentTypeFlagColumn} = 1" : string.Empty;
        var aliasedWhere = "d.IS_DELETED = 0" + publishedDossierFilter + documentTypeFilter + " AND de.EquipmentId = :EquipmentId";
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            aliasedWhere += " AND d.NAME LIKE :Keyword";

        var countParams = new DynamicParameters();
        countParams.Add("EquipmentId", equipmentId.ToString());
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            countParams.Add("Keyword", $"%{filter.Keyword}%");

        var countSql = $@"
            SELECT COUNT(*) 
            FROM DOCUMENTS d 
            {dossierJoin}
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.DOSSIER_ID = de.DossierId
            INNER JOIN DOCUMENT_TYPES dt ON {DocumentTypeActiveJoin}
            WHERE {aliasedWhere}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, countParams);

        var offset = (filter.Page - 1) * filter.PageSize;
        var listSql = $@"
            SELECT 
                d.ID,
                d.NAME,
                d.FOLDER_ID AS FolderId,
                d.DOSSIER_ID AS DossierId,
                d.DOCUMENT_TYPE_ID AS DocumentTypeId,
                dt.NAME AS DocumentTypeName,
                dt.IS_EQUIPMENT_PROFILE AS IsEquipmentProfile,
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
            {dossierJoin}
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.DOSSIER_ID = de.DossierId
            {DocumentCreatorJoin}
            INNER JOIN DOCUMENT_TYPES dt ON {DocumentTypeActiveJoin}
            LEFT JOIN (
                SELECT dv.DOCUMENT_ID, dv.ID AS LATEST_VERSION_ID, dv.FILE_SIZE, dv.MIME_TYPE
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN (
                    SELECT DOCUMENT_ID, MAX(VERSION_NUMBER) AS MAX_VER
                    FROM DOCUMENT_VERSIONS
                    WHERE IS_DELETED = 0
                    GROUP BY DOCUMENT_ID
                ) m ON dv.DOCUMENT_ID = m.DOCUMENT_ID AND dv.VERSION_NUMBER = m.MAX_VER
                WHERE dv.IS_DELETED = 0
            ) latest ON d.ID = latest.DOCUMENT_ID
            LEFT JOIN (
                SELECT ranked.*
                FROM (
                    SELECT p.*,
                           ROW_NUMBER() OVER (
                               PARTITION BY p.DOCUMENT_VERSION_ID
                               ORDER BY p.CREATED_DATE DESC, p.ID DESC
                           ) AS RN
                    FROM DOCUMENT_OCR_PROGRESS p
                    WHERE p.IS_DELETED = 0
                ) ranked
                WHERE ranked.RN = 1
            ) ocr ON latest.LATEST_VERSION_ID = ocr.DOCUMENT_VERSION_ID
            LEFT JOIN (
                SELECT ranked.*
                FROM (
                    SELECT e.*,
                           ROW_NUMBER() OVER (
                               PARTITION BY e.DOCUMENT_VERSION_ID, e.EQUIPMENT_ID
                               ORDER BY e.CREATED_DATE DESC, e.ID DESC
                           ) AS RN
                    FROM DOCUMENT_EXTRACTION_RESULTS e
                    WHERE e.IS_DELETED = 0
                ) ranked
                WHERE ranked.RN = 1
            ) ext ON latest.LATEST_VERSION_ID = ext.DOCUMENT_VERSION_ID AND ext.EQUIPMENT_ID = :EquipmentId
            WHERE {aliasedWhere}
            ORDER BY d.CREATED_DATE DESC
            OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        var listParams = new DynamicParameters();
        listParams.Add("EquipmentId", equipmentId.ToString());
        listParams.Add("Offset", offset);
        listParams.Add("PageSize", filter.PageSize);
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            listParams.Add("Keyword", $"%{filter.Keyword}%");

        var rows = await _connection.QueryAsync<DossierDocumentListRow>(listSql, listParams);
        var items = rows.Select(MapDossierDocumentListRow).ToList();

        return (items, totalCount);
    }
}

