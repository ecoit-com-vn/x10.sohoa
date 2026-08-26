using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

using Infrastructure = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;
using OrganizationDto = EvnHanoi.EquipmentService.Core.Entities.OrganizationDto;

public class InfrastructureRepository : IInfrastructureRepository
{
    private readonly IDbConnection _connection;

    public InfrastructureRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<Infrastructure?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT i.{nameof(Infrastructure.Id)},
                            i.{nameof(Infrastructure.Code)},
                            i.{nameof(Infrastructure.Name)},
                            i.{nameof(Infrastructure.Address)},
                            i.INFRA_TYPE_ID as {nameof(Infrastructure.InfraTypeId)},
                            i.UNIT_ID as {nameof(Infrastructure.UnitId)},
                            i.GRIDTYPEID as {nameof(Infrastructure.GridTypeId)},
                            i.OPERATION_DATE as {nameof(Infrastructure.OperationDate)},
                            i.IS_ACTIVE as {nameof(Infrastructure.IsActive)},
                            i.{nameof(Infrastructure.CreatedBy)},
                            i.{nameof(Infrastructure.CreatedDate)},
                            i.{nameof(Infrastructure.ModifiedBy)},
                            i.{nameof(Infrastructure.ModifiedDate)},
                            i.{nameof(Infrastructure.IsDeleted)},
                            it.NAME as {nameof(Infrastructure.InfraTypeName)},
                            u.NAME as {nameof(Infrastructure.UnitName)},
                            u.Id as OrgId,
                            u.Code as OrgCode,
                            u.Name as OrgName,
                            (SELECT COUNT(1) FROM EQUIPMENTS eq WHERE eq.INFRASTRUCTURE_ID = i.{nameof(Infrastructure.Id)} AND eq.IsDeleted = 0) AS {nameof(Infrastructure.EquipmentCount)}
                     FROM INFRASTRUCTURE i
                     LEFT JOIN INFRASTRUCTURE_TYPE it ON i.INFRA_TYPE_ID = it.ID
                     LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                     WHERE i.{nameof(Infrastructure.Id)} = :Id AND i.{nameof(Infrastructure.IsDeleted)} = 0";

        var result = await _connection.QueryAsync<Infrastructure, OrganizationDto, Infrastructure>(
            sql, 
            (infra, org) => {
                if (org != null && org.Id > 0) {
                    infra.Organization = org;
                }
                return infra;
            },
            new { Id = id.ToString() },
            splitOn: "OrgId"
        );
        return result.FirstOrDefault();
    }

    public async Task<Infrastructure?> GetByCodeAsync(string code)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT i.{nameof(Infrastructure.Id)},
                            i.{nameof(Infrastructure.Code)},
                            i.{nameof(Infrastructure.Name)},
                            i.{nameof(Infrastructure.Address)},
                            i.INFRA_TYPE_ID as {nameof(Infrastructure.InfraTypeId)},
                            i.UNIT_ID as {nameof(Infrastructure.UnitId)},
                            i.GRIDTYPEID as {nameof(Infrastructure.GridTypeId)},
                            i.OPERATION_DATE as {nameof(Infrastructure.OperationDate)},
                            i.IS_ACTIVE as {nameof(Infrastructure.IsActive)},
                            i.{nameof(Infrastructure.CreatedBy)},
                            i.{nameof(Infrastructure.CreatedDate)},
                            i.{nameof(Infrastructure.ModifiedBy)},
                            i.{nameof(Infrastructure.ModifiedDate)},
                            i.{nameof(Infrastructure.IsDeleted)},
                            it.NAME as {nameof(Infrastructure.InfraTypeName)},
                            u.NAME as {nameof(Infrastructure.UnitName)},
                            u.Id as OrgId,
                            u.Code as OrgCode,
                            u.Name as OrgName,
                            (SELECT COUNT(1) FROM EQUIPMENTS eq WHERE eq.INFRASTRUCTURE_ID = i.{nameof(Infrastructure.Id)} AND eq.IsDeleted = 0) AS {nameof(Infrastructure.EquipmentCount)}
                     FROM INFRASTRUCTURE i
                     LEFT JOIN INFRASTRUCTURE_TYPE it ON i.INFRA_TYPE_ID = it.ID
                     LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                     WHERE LOWER(i.{nameof(Infrastructure.Code)}) = :Code AND i.{nameof(Infrastructure.IsDeleted)} = 0";

        var result = await _connection.QueryAsync<Infrastructure, OrganizationDto, Infrastructure>(
            sql, 
            (infra, org) => {
                if (org != null && org.Id > 0) {
                    infra.Organization = org;
                }
                return infra;
            },
            new { Code = code.ToLower().Trim() },
            splitOn: "OrgId"
        );
        return result.FirstOrDefault();
    }

    public async Task<(IEnumerable<Infrastructure> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        int infraTypeId, 
        string? keyword, 
        int? status,
        IEnumerable<long>? unitIds = null,
        long? unitId = null,
        int? gridTypeId = null,
        DateTime? fromOperationDate = null,
        DateTime? toOperationDate = null)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sqlBase = $@"FROM INFRASTRUCTURE i
                          LEFT JOIN INFRASTRUCTURE_TYPE it ON i.INFRA_TYPE_ID = it.ID
                          LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                          WHERE i.{nameof(Infrastructure.IsDeleted)} = 0 AND i.INFRA_TYPE_ID = :InfraTypeId";

        var parameters = new DynamicParameters();
        parameters.Add("InfraTypeId", infraTypeId);

        if (!string.IsNullOrEmpty(keyword))
        {
            sqlBase += $" AND (LOWER(i.{nameof(Infrastructure.Code)}) LIKE :Keyword OR LOWER(i.{nameof(Infrastructure.Name)}) LIKE :Keyword)";
            parameters.Add("Keyword", $"%{keyword.ToLower().Trim()}%");
        }

        if (status.HasValue)
        {
            sqlBase += $" AND i.IS_ACTIVE = :Status";
            parameters.Add("Status", status.Value);
        }

        if (gridTypeId.HasValue)
        {
            sqlBase += $" AND i.GRIDTYPEID = :GridTypeId";
            parameters.Add("GridTypeId", gridTypeId.Value);
        }

        if (fromOperationDate.HasValue)
        {
            sqlBase += " AND i.OPERATION_DATE >= :FromOperationDate";
            parameters.Add("FromOperationDate", fromOperationDate.Value.Date);
        }

        if (toOperationDate.HasValue)
        {
            sqlBase += " AND i.OPERATION_DATE < :ToOperationDateExclusive";
            parameters.Add("ToOperationDateExclusive", toOperationDate.Value.Date.AddDays(1));
        }

        if (unitId.HasValue && unitId.Value > 0)
        {
            sqlBase += $" AND i.UNIT_ID = :UnitId";
            parameters.Add("UnitId", unitId.Value);
        }
        else if (unitIds != null && unitIds.Any())
        {
            sqlBase += $" AND i.UNIT_ID IN :UnitIds";
            parameters.Add("UnitIds", unitIds);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectSql = $@"SELECT i.{nameof(Infrastructure.Id)},
                           i.{nameof(Infrastructure.Code)},
                           i.{nameof(Infrastructure.Name)},
                           i.{nameof(Infrastructure.Address)},
                           i.INFRA_TYPE_ID AS {nameof(Infrastructure.InfraTypeId)},
                           i.UNIT_ID AS {nameof(Infrastructure.UnitId)},
                           i.GRIDTYPEID AS {nameof(Infrastructure.GridTypeId)},
                           i.OPERATION_DATE AS {nameof(Infrastructure.OperationDate)},
                           i.IS_ACTIVE AS {nameof(Infrastructure.IsActive)},
                           i.{nameof(Infrastructure.CreatedBy)},
                           i.{nameof(Infrastructure.CreatedDate)},
                           i.{nameof(Infrastructure.ModifiedBy)},
                           i.{nameof(Infrastructure.ModifiedDate)},
                           i.{nameof(Infrastructure.IsDeleted)},
                           it.NAME AS {nameof(Infrastructure.InfraTypeName)},
                           u.NAME AS {nameof(Infrastructure.UnitName)},
                           u.Id AS OrgId,
                           u.Code AS OrgCode,
                           u.Name AS OrgName,
                           (SELECT COUNT(1)
                              FROM EQUIPMENTS eq
                             WHERE eq.INFRASTRUCTURE_ID = i.{nameof(Infrastructure.Id)}
                               AND eq.IsDeleted = 0) AS {nameof(Infrastructure.EquipmentCount)}
                   {sqlBase}
                   ORDER BY i.IS_ACTIVE DESC,
                            i.{nameof(Infrastructure.Code)} ASC,
                            i.{nameof(Infrastructure.CreatedDate)} DESC
                   OFFSET :Offset ROWS
                   FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var items = await _connection.QueryAsync<Infrastructure, OrganizationDto, Infrastructure>(
            selectSql,
            (infra, org) => {
                if (org != null && org.Id > 0) {
                    infra.Organization = org;
                }
                return infra;
            },
            parameters,
            splitOn: "OrgId"
        );

        return (items, totalCount);
    }

    public async Task<Guid> CreateAsync(Infrastructure infrastructure)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        if (infrastructure.Id == Guid.Empty)
        {
            infrastructure.Id = Guid.Parse(EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid());
        }

        var sql = $@"INSERT INTO INFRASTRUCTURE (
                        {nameof(Infrastructure.Id)},
                        {nameof(Infrastructure.Code)},
                        {nameof(Infrastructure.Name)},
                        {nameof(Infrastructure.Address)},
                        INFRA_TYPE_ID,
                        UNIT_ID,
                        GRIDTYPEID,
                        OPERATION_DATE,
                        IS_ACTIVE,
                        {nameof(Infrastructure.CreatedBy)},
                        {nameof(Infrastructure.CreatedDate)},
                        {nameof(Infrastructure.IsDeleted)}
                    )
                    VALUES (:Id, :Code, :Name, :Address, :InfraTypeId, :UnitId, :GridTypeId, :OperationDate, :IsActive, :CreatedBy, :CreatedDate, :IsDeleted)";

        var param = new
        {
            Id = infrastructure.Id.ToString(),
            infrastructure.Code,
            infrastructure.Name,
            infrastructure.Address,
            infrastructure.InfraTypeId,
            infrastructure.UnitId,
            infrastructure.GridTypeId,
            infrastructure.OperationDate,
            IsActive = infrastructure.IsActive ? 1 : 0,
            infrastructure.CreatedBy,
            infrastructure.CreatedDate,
            IsDeleted = infrastructure.IsDeleted ? 1 : 0
        };

        await _connection.ExecuteAsync(sql, param);
        return infrastructure.Id;
    }

    public async Task<bool> UpdateAsync(Infrastructure infrastructure)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"UPDATE INFRASTRUCTURE
                    SET {nameof(Infrastructure.Code)} = :Code,
                        {nameof(Infrastructure.Name)} = :Name,
                        {nameof(Infrastructure.Address)} = :Address,
                        INFRA_TYPE_ID = :InfraTypeId,
                        UNIT_ID = :UnitId,
                        GRIDTYPEID = :GridTypeId,
                        OPERATION_DATE = :OperationDate,
                        IS_ACTIVE = :IsActive,
                        {nameof(Infrastructure.ModifiedBy)} = :ModifiedBy,
                        {nameof(Infrastructure.ModifiedDate)} = :ModifiedDate
                    WHERE {nameof(Infrastructure.Id)} = :Id AND {nameof(Infrastructure.IsDeleted)} = 0";

        var param = new
        {
            Id = infrastructure.Id.ToString(),
            infrastructure.Code,
            infrastructure.Name,
            infrastructure.Address,
            infrastructure.InfraTypeId,
            infrastructure.UnitId,
            infrastructure.GridTypeId,
            infrastructure.OperationDate,
            IsActive = infrastructure.IsActive ? 1 : 0,
            infrastructure.ModifiedBy,
            infrastructure.ModifiedDate
        };

        var affected = await _connection.ExecuteAsync(sql, param);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"UPDATE INFRASTRUCTURE
                    SET {nameof(Infrastructure.IsDeleted)} = 1
                    WHERE {nameof(Infrastructure.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, new { Id = id.ToString() });
        return affected > 0;
    }

    public async Task<(Guid Id, bool WasCreated)> UpsertFromPmisAsync(
        int infraTypeId, string pmisCode, string code, string name, string? address, string? unitCode, DateTime? operationDate, int? gridTypeId = null)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        // Mã đơn vị PMIS (vd. "HN0200") KHÔNG khớp trực tiếp ORGANIZATION_UNIT.Code (vd. "HN02") —
        // xác nhận bằng dữ liệu thật, xem PMIS_UNIT_CODE_MAPPING (Migration0051). Không fallback so
        // khớp trực tiếp Code = Code để tránh khớp nhầm ngẫu nhiên.
        long? unitId = null;
        if (!string.IsNullOrWhiteSpace(unitCode))
        {
            unitId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT UnitId FROM PMIS_UNIT_CODE_MAPPING WHERE PmisUnitCode = :Code AND IsDeleted = 0", new { Code = unitCode });
        }

        var existingId = await _connection.QuerySingleOrDefaultAsync<string?>(
            $"SELECT {nameof(Infrastructure.Id)} FROM INFRASTRUCTURE WHERE PMIS_CODE = :PmisCode AND {nameof(Infrastructure.IsDeleted)} = 0",
            new { PmisCode = pmisCode });

        if (existingId != null)
        {
            var updateSql = $@"UPDATE INFRASTRUCTURE
                        SET {nameof(Infrastructure.Code)} = :Code,
                            {nameof(Infrastructure.Name)} = :Name,
                            {nameof(Infrastructure.Address)} = :Address,
                            UNIT_ID = :UnitId,
                            OPERATION_DATE = :OperationDate,
                            GRIDTYPEID = COALESCE(:GridTypeId, GRIDTYPEID),
                            LAST_SYNCED_FROM_PMIS_AT = SYSTIMESTAMP,
                            {nameof(Infrastructure.ModifiedBy)} = :ModifiedBy,
                            {nameof(Infrastructure.ModifiedDate)} = SYSTIMESTAMP
                        WHERE {nameof(Infrastructure.Id)} = :Id";

            await _connection.ExecuteAsync(updateSql, new
            {
                Id = existingId,
                Code = code,
                Name = name,
                Address = address,
                UnitId = unitId,
                OperationDate = operationDate,
                GridTypeId = gridTypeId,
                ModifiedBy = "PMIS_SYNC"
            });
            return (Guid.Parse(existingId), false);
        }

        var newId = Guid.Parse(EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid());
        var insertSql = $@"INSERT INTO INFRASTRUCTURE (
                        {nameof(Infrastructure.Id)}, {nameof(Infrastructure.Code)}, {nameof(Infrastructure.Name)},
                        {nameof(Infrastructure.Address)}, INFRA_TYPE_ID, UNIT_ID, OPERATION_DATE, GRIDTYPEID, IS_ACTIVE,
                        PMIS_CODE, LAST_SYNCED_FROM_PMIS_AT,
                        {nameof(Infrastructure.CreatedBy)}, {nameof(Infrastructure.CreatedDate)}, {nameof(Infrastructure.IsDeleted)}
                    ) VALUES (
                        :Id, :Code, :Name, :Address, :InfraTypeId, :UnitId, :OperationDate, :GridTypeId, 1,
                        :PmisCode, SYSTIMESTAMP, :CreatedBy, SYSTIMESTAMP, 0
                    )";

        await _connection.ExecuteAsync(insertSql, new
        {
            Id = newId.ToString(),
            Code = code,
            Name = name,
            Address = address,
            InfraTypeId = infraTypeId,
            UnitId = unitId,
            OperationDate = operationDate,
            GridTypeId = gridTypeId,
            PmisCode = pmisCode,
            CreatedBy = "PMIS_SYNC"
        });
        return (newId, true);
    }

    private class SyncedPmisCodeRow
    {
        public string PmisCode { get; set; } = string.Empty;
        public int InfraTypeId { get; set; }
    }

    public async Task<IEnumerable<(string PmisCode, int InfraTypeId)>> GetSyncedPmisCodesAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var rows = await _connection.QueryAsync<SyncedPmisCodeRow>(
            $"SELECT PMIS_CODE AS PmisCode, INFRA_TYPE_ID AS InfraTypeId FROM INFRASTRUCTURE WHERE PMIS_CODE IS NOT NULL AND {nameof(Infrastructure.IsDeleted)} = 0");
        return rows.Select(r => (r.PmisCode, r.InfraTypeId));
    }
}
