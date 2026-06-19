using System.Data;
using Dapper;
using EvnHanoi.NotificationService.Models;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.NotificationService.Repositories;

/// <summary>
/// Truy vấn Oracle để enrich dossier trước khi index ES.
/// Tạo connection riêng mỗi lần gọi — an toàn cho BackgroundService worker.
/// </summary>
public class DossierEnrichmentRepository : IDossierEnrichmentRepository
{
    private readonly string _connectionString;

    public DossierEnrichmentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection chưa được cấu hình cho NotificationService.");
    }

    public Task<DossierEnrichmentData?> GetByIdAsync(string dossierId) =>
        WithConnectionAsync(async connection =>
        {
            const string sql = """
                SELECT
                    d.Id,
                    d.GridTypeId,
                    gt.Name AS GridTypeName,
                    d.InfrastructureId,
                    i.NAME AS InfrastructureName,
                    i.CODE AS InfrastructureCode,
                    i.UNIT_ID AS UnitId,
                    d.DossierSetId,
                    ds.Name AS DossierSetName,
                    d.DossierTypeId,
                    dt.Name AS DossierTypeName,
                    d.FormDataJson,
                    d.Status,
                    d.WorkflowStatusName,
                    d.WorkflowInstanceId,
                    d.CreatorId,
                    d.CreatorUsername,
                    d.CreatorName,
                    d.CreatedDate,
                    d.ModifiedDate,
                    d.IsDeleted,
                    COALESCE((
                        SELECT MAX(v.VersionNumber)
                        FROM DOSSIER_VERSIONS v
                        WHERE v.DossierId = d.Id
                    ), 0) AS CurrentVersionNumber
                FROM DOSSIERS d
                LEFT JOIN GridTypes gt ON d.GridTypeId = gt.Id
                LEFT JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                LEFT JOIN DOSSIER_SETS ds ON d.DossierSetId = ds.Id
                LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.Id
                WHERE d.Id = :DossierId
                """;

            return await connection.QuerySingleOrDefaultAsync<DossierEnrichmentData>(
                sql, new { DossierId = dossierId });
        });

    public Task<IEnumerable<string>> GetAllIdsAsync() =>
        WithConnectionAsync(connection =>
            connection.QueryAsync<string>("SELECT Id FROM DOSSIERS"));

    public Task<IEnumerable<BhsCatalogDefinition>> GetBhsCatalogDefinitionsAsync() =>
        WithConnectionAsync(async connection =>
        {
            const string sql = """
                SELECT c.Code, c.Name, c.Priority
                FROM CATALOG c
                INNER JOIN CATALOG_TYPE ct ON c.CatalogTypeId = ct.Id
                WHERE ct.Code = 'BHS'
                  AND c.IsDeleted = 0
                  AND ct.IsDeleted = 0
                ORDER BY c.Priority ASC, c.Name ASC
                """;

            return await connection.QueryAsync<BhsCatalogDefinition>(sql);
        });

    public Task<IEnumerable<DossierEquipmentEnrichment>> GetEquipmentsAsync(string dossierId) =>
        WithConnectionAsync(async connection =>
        {
            const string sql = """
                SELECT
                    de.EquipmentId,
                    e.CODE AS EquipmentCode,
                    e.NAME AS EquipmentName,
                    e.SerialNumber
                FROM DOSSIER_EQUIPMENTS de
                INNER JOIN Equipments e ON de.EquipmentId = e.Id
                WHERE de.DossierId = :DossierId
                """;

            return await connection.QueryAsync<DossierEquipmentEnrichment>(
                sql, new { DossierId = dossierId });
        });

    private async Task<T> WithConnectionAsync<T>(Func<IDbConnection, Task<T>> action)
    {
        await using var connection = new OracleConnection(_connectionString);
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        return await action(connection);
    }
}
