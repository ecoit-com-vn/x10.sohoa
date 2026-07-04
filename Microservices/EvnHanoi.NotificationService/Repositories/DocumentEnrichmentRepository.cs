using System.Data;
using Dapper;
using EvnHanoi.NotificationService.Models;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.NotificationService.Repositories;

public class DocumentEnrichmentRepository : IDocumentEnrichmentRepository
{
    private readonly string _connectionString;

    public DocumentEnrichmentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection chưa được cấu hình cho NotificationService.");
    }

    public Task<DocumentEnrichmentData?> GetByVersionIdAsync(string documentVersionId) =>
        WithConnectionAsync(async connection =>
        {
            const string sql = """
                SELECT
                    dv.ID AS DocumentVersionId,
                    dv.DOCUMENT_ID AS DocumentId,
                    dv.VERSION_NUMBER AS VersionNumber,
                    dv.FILE_PATH AS FilePath,
                    NVL(dv.FILE_SIZE, 0) AS FileSize,
                    dv.MIME_TYPE AS MimeType,
                    d.NAME AS DocumentName,
                    d.DOSSIER_ID AS DossierId,
                    d.DOCUMENT_TYPE_ID AS DocumentTypeId,
                    dt.NAME AS DocumentTypeName,
                    NVL(d.IS_DELETED, 0) AS DocumentIsDeleted,
                    dos.STATUS_ID AS StatusId,
                    dstat.CODE AS StatusCode,
                    dos.DossierTypeId AS DossierTypeId,
                    dtype.Name AS DossierTypeName,
                    dos.InfrastructureId AS InfrastructureId,
                    inf.NAME AS InfrastructureName,
                    inf.CODE AS InfrastructureCode,
                    inf.UNIT_ID AS UnitId,
                    dos.PUBLISHSTATUSID AS PublishStatusId,
                    ps.CODE AS PublishStatusCode,
                    ext.MERGED_DATA_JSON AS MergedDataJson,
                    ocr.MODIFIED_DATE AS OcrCompletedAt,
                    ocr.BUCKET_NAME AS BucketName
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID
                LEFT JOIN DOCUMENT_TYPES dt ON dt.ID = d.DOCUMENT_TYPE_ID AND dt.IsDeleted = 0
                LEFT JOIN DOSSIERS dos ON dos.Id = d.DOSSIER_ID AND dos.IsDeleted = 0
                LEFT JOIN DOSSIER_STATUSES dstat ON dos.STATUS_ID = dstat.ID
                LEFT JOIN PUBLISH_STATUSES ps ON dos.PUBLISHSTATUSID = ps.ID
                LEFT JOIN DOSSIER_TYPES dtype ON dos.DossierTypeId = dtype.Id
                LEFT JOIN INFRASTRUCTURE inf ON dos.InfrastructureId = inf.ID
                LEFT JOIN (
                    SELECT DOCUMENT_VERSION_ID, MERGED_DATA_JSON
                    FROM (
                        SELECT e.DOCUMENT_VERSION_ID, e.MERGED_DATA_JSON,
                               ROW_NUMBER() OVER (
                                   PARTITION BY e.DOCUMENT_VERSION_ID
                                   ORDER BY e.CREATED_DATE DESC) AS RN
                        FROM DOCUMENT_EXTRACTION_RESULTS e
                        WHERE e.IS_DELETED = 0
                    ) ranked
                    WHERE RN = 1
                ) ext ON ext.DOCUMENT_VERSION_ID = dv.ID
                LEFT JOIN (
                    SELECT DOCUMENT_VERSION_ID, MODIFIED_DATE, BUCKET_NAME
                    FROM (
                        SELECT p.DOCUMENT_VERSION_ID, p.MODIFIED_DATE, p.BUCKET_NAME,
                               ROW_NUMBER() OVER (
                                   PARTITION BY p.DOCUMENT_VERSION_ID
                                   ORDER BY p.CREATED_DATE DESC) AS RN
                        FROM DOCUMENT_OCR_PROGRESS p
                        WHERE p.IS_DELETED = 0
                    ) ranked
                    WHERE RN = 1
                ) ocr ON ocr.DOCUMENT_VERSION_ID = dv.ID
                WHERE LOWER(dv.ID) = LOWER(:DocumentVersionId)
                  AND dv.IS_DELETED = 0
                """;

            return await connection.QueryFirstOrDefaultAsync<DocumentEnrichmentData>(
                sql,
                new { DocumentVersionId = documentVersionId.Trim() });
        });

    public Task<IEnumerable<string>> GetEquipmentNamesByDossierIdAsync(string dossierId) =>
        WithConnectionAsync(async connection =>
        {
            const string sql = """
                SELECT e.NAME AS EquipmentName
                FROM DOSSIER_EQUIPMENTS de
                INNER JOIN Equipments e ON de.EquipmentId = e.Id
                WHERE LOWER(de.DossierId) = LOWER(:DossierId)
                ORDER BY e.NAME
                """;

            return await connection.QueryAsync<string>(sql, new { DossierId = dossierId.Trim() });
        });

    public Task<IEnumerable<string>> GetIndexableVersionIdsAsync() =>
        WithConnectionAsync(async connection =>
        {
            const string sql = """
                SELECT dv.ID
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID AND d.IS_DELETED = 0
                INNER JOIN (
                    SELECT DOCUMENT_VERSION_ID
                    FROM DOCUMENT_OCR_PROGRESS
                    WHERE IS_DELETED = 0
                      AND STATUS IN ('OcrCompleted', 'Completed', 'Extracting')
                    GROUP BY DOCUMENT_VERSION_ID
                ) ocr ON ocr.DOCUMENT_VERSION_ID = dv.ID
                WHERE dv.IS_DELETED = 0
                ORDER BY dv.CREATED_DATE DESC
                """;

            return await connection.QueryAsync<string>(sql);
        });

    public Task<IEnumerable<string>> GetPublishedIndexableVersionIdsAsync() =>
        WithConnectionAsync(async connection =>
        {
            const string sql = """
                SELECT dv.ID
                FROM DOCUMENT_VERSIONS dv
                INNER JOIN DOCUMENTS d ON d.ID = dv.DOCUMENT_ID AND d.IS_DELETED = 0
                INNER JOIN DOSSIERS dos ON dos.Id = d.DOSSIER_ID AND dos.IsDeleted = 0
                INNER JOIN (
                    SELECT DOCUMENT_VERSION_ID
                    FROM DOCUMENT_OCR_PROGRESS
                    WHERE IS_DELETED = 0
                      AND STATUS IN ('OcrCompleted', 'Completed', 'Extracting')
                    GROUP BY DOCUMENT_VERSION_ID
                ) ocr ON ocr.DOCUMENT_VERSION_ID = dv.ID
                WHERE dv.IS_DELETED = 0
                  AND dos.STATUS_ID = :ApprovedStatusId
                  AND dos.PUBLISHSTATUSID = :PublishedStatusId
                ORDER BY dv.CREATED_DATE DESC
                """;

            return await connection.QueryAsync<string>(sql, new
            {
                ApprovedStatusId = DocumentSearchConstants.ApprovedStatusId,
                PublishedStatusId = DocumentSearchConstants.PublishedStatusId
            });
        });

    private async Task<T> WithConnectionAsync<T>(Func<IDbConnection, Task<T>> action)
    {
        await using var connection = new OracleConnection(_connectionString);
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        return await action(connection);
    }
}
