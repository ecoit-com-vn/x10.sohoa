using System.Data;
using System.Text.Json;
using Dapper;
using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using EvnHanoi.ReportService.Infrastructure.Services;

namespace EvnHanoi.ReportService.Infrastructure.Repositories;

public class ReportDossierDetailRepository : IReportDossierDetailRepository
{
    private readonly IDbConnection _connection;
    private readonly IReportFileDownloadTokenService _downloadTokenService;

    public ReportDossierDetailRepository(
        IDbConnection connection,
        IReportFileDownloadTokenService downloadTokenService)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _downloadTokenService = downloadTokenService;
    }

    public async Task<bool> IsPublishedDossierAccessibleAsync(Guid dossierId, long? unitId)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        parameters.Add("DossierId", dossierId.ToString());

        var sql = @"SELECT COUNT(1)
                    FROM DOSSIERS d
                    INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                    WHERE d.Id = :DossierId
                      AND d.IsDeleted = 0
                      AND d.STATUS_ID = 6
                      AND d.PUBLISHSTATUSID = 2";

        AppendUnitScope(ref sql, parameters, unitId);

        var count = await _connection.ExecuteScalarAsync<int>(sql, parameters);
        return count > 0;
    }

    public async Task<ReportDossierDetailDto?> GetPublishedDetailAsync(Guid id)
    {
        EnsureOpen();
        const string sql = @"SELECT
                        d.Id,
                        d.GridTypeId,
                        gt.Name AS GridTypeName,
                        d.InfrastructureId,
                        i.NAME AS InfrastructureName,
                        i.CODE AS InfrastructureCode,
                        d.DossierSetId,
                        ds.NAME AS DossierSetName,
                        d.DossierTypeId,
                        dt.NAME AS DossierTypeName,
                        dt.FORM_ID AS FormId,
                        d.FormDataJson,
                        d.STATUS_ID AS StatusId,
                        dstat.CODE AS StatusCode,
                        dstat.NAME AS StatusName,
                        d.KIND_ID AS KindId,
                        d.WorkflowInstanceId,
                        d.WorkflowStatusName,
                        d.RowVersion,
                        d.CreatedBy,
                        d.CreatedDate,
                        d.ModifiedBy,
                        d.ModifiedDate,
                        d.PUBLISHSTATUSID AS PublishStatusId,
                        ps.CODE AS PublishStatusCode,
                        ps.NAME AS PublishStatusName,
                        d.CreatorId AS Id,
                        d.CreatorUsername AS Username,
                        d.CreatorName AS Name
                     FROM DOSSIERS d
                     LEFT JOIN GridTypes gt ON d.GridTypeId = gt.Id
                     LEFT JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                     LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
                     LEFT JOIN DOSSIER_SETS ds ON d.DossierSetId = ds.ID
                     LEFT JOIN PUBLISH_STATUSES ps ON d.PUBLISHSTATUSID = ps.ID
                     LEFT JOIN DOSSIER_STATUSES dstat ON d.STATUS_ID = dstat.ID
                     WHERE d.Id = :Id AND d.IsDeleted = 0";

        var dossierList = await _connection.QueryAsync<ReportDossierDetailDto, ReportCreatorInfo, ReportDossierDetailDto>(
            sql,
            (dossierDto, creatorDto) =>
            {
                dossierDto.Creator = creatorDto;
                return dossierDto;
            },
            new { Id = id.ToString() },
            splitOn: "Id");

        var dossier = dossierList.FirstOrDefault();
        if (dossier is null)
            return null;

        dossier.Equipments = (await GetEquipmentsAsync(id)).ToList();
        return dossier;
    }

    public async Task<(IEnumerable<ReportDocumentListItemDto> Items, int TotalCount)> GetDocumentsAsync(
        Guid dossierId,
        ReportDocumentFilterDto filter)
    {
        EnsureOpen();

        var aliasedWhere = "d.IS_DELETED = 0 AND d.DOSSIER_ID = :DossierId";
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            aliasedWhere += " AND d.NAME LIKE :Keyword";

        var countParams = new DynamicParameters();
        countParams.Add("DossierId", dossierId.ToString());
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            countParams.Add("Keyword", $"%{filter.Keyword}%");

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM DOCUMENTS d WHERE {aliasedWhere}",
            countParams);

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
                NVL(d.CREATOR_NAME, d.CREATED_BY) AS CreatedByName,
                d.CREATED_DATE AS CreatedDate,
                NVL(latest.FILE_SIZE, 0) AS FileSize,
                latest.MIME_TYPE AS MimeType,
                latest.LATEST_VERSION_ID AS LatestVersionId
            FROM DOCUMENTS d
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

        var rows = await _connection.QueryAsync<ReportDocumentListRow>(listSql, listParams);
        var items = rows.Select(MapDocumentRow);
        return (items, totalCount);
    }

    public async Task<ReportDownloadTokenResponse?> CreateDocumentDownloadTokenAsync(
        Guid dossierId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        if (!await VersionBelongsToDossierAsync(versionId, dossierId))
            return null;

        const string sql = @"
            SELECT dv.FILE_PATH AS FilePath, dv.MIME_TYPE AS MimeType, d.NAME AS FileName
            FROM DOCUMENT_VERSIONS dv
            INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID
            WHERE dv.ID = :VersionId
              AND dv.IS_DELETED = 0
              AND d.IS_DELETED = 0";

        var row = await _connection.QuerySingleOrDefaultAsync<DocumentVersionDownloadRow>(
            sql,
            new { VersionId = versionId.ToString() });

        if (row is null || string.IsNullOrWhiteSpace(row.FilePath))
            return null;

        var tokenResponse = await _downloadTokenService.CreateTokenAsync(
            row.FilePath,
            row.FileName,
            row.MimeType ?? "application/octet-stream",
            cancellationToken: cancellationToken);

        var downloadPath = $"/api/v1/reports/files/download?token={tokenResponse.Token}";
        tokenResponse.Url = downloadPath;
        tokenResponse.DownloadUrl = downloadPath;
        return tokenResponse;
    }

    private async Task<IEnumerable<ReportDossierEquipmentDto>> GetEquipmentsAsync(Guid dossierId)
    {
        const string sql = @"SELECT
                        de.EquipmentId,
                        e.CODE AS EquipmentCode,
                        e.NAME AS EquipmentName,
                        e.SerialNumber,
                        et.NAME AS EquipmentTypeName,
                        i.NAME AS InfrastructureName
                     FROM DOSSIER_EQUIPMENTS de
                     INNER JOIN Equipments e ON de.EquipmentId = e.Id
                     LEFT JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                     LEFT JOIN INFRASTRUCTURE i ON e.Infrastructure_Id = i.ID
                     WHERE de.DossierId = :DossierId";

        return await _connection.QueryAsync<ReportDossierEquipmentDto>(sql, new { DossierId = dossierId.ToString() });
    }

    private async Task<bool> VersionBelongsToDossierAsync(Guid versionId, Guid dossierId)
    {
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

    private static void AppendUnitScope(ref string sql, DynamicParameters parameters, long? unitId)
    {
        if (!unitId.HasValue)
            return;

        sql += @" AND i.UNIT_ID IN (
            SELECT Id
            FROM ORGANIZATION_UNIT
            START WITH Id = :UnitId
            CONNECT BY PRIOR Id = ParentId
        )";
        parameters.Add("UnitId", unitId.Value);
    }

    private static ReportDocumentListItemDto MapDocumentRow(ReportDocumentListRow row) =>
        new()
        {
            Id = Guid.Parse(row.Id),
            Name = row.Name,
            FolderId = string.IsNullOrEmpty(row.FolderId) ? null : Guid.Parse(row.FolderId),
            DossierId = string.IsNullOrEmpty(row.DossierId) ? null : Guid.Parse(row.DossierId),
            DocumentTypeId = string.IsNullOrEmpty(row.DocumentTypeId) ? null : Guid.Parse(row.DocumentTypeId),
            DocumentTypeName = row.DocumentTypeName,
            CreatedBy = row.CreatedBy,
            CreatedByName = row.CreatedByName,
            CreatedDate = row.CreatedDate,
            FileSize = row.FileSize,
            MimeType = row.MimeType,
            LatestVersionId = string.IsNullOrEmpty(row.LatestVersionId) ? null : Guid.Parse(row.LatestVersionId)
        };

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
    }

    private sealed class ReportDocumentListRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FolderId { get; set; }
        public string? DossierId { get; set; }
        public string? DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public long FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? LatestVersionId { get; set; }
    }

    private sealed class DocumentVersionDownloadRow
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? MimeType { get; set; }
    }
}
