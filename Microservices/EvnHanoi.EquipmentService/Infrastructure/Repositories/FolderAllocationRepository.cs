using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class FolderAllocationRepository : IFolderAllocationRepository
{
    private readonly IDbConnection _connection;

    public FolderAllocationRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<(IEnumerable<FolderAllocationListItemDto> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? keyword,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        IEnumerable<long> unitScopeIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var unitIdsArray = unitScopeIds.ToArray();
        if (unitIdsArray.Length == 0)
        {
            return (Enumerable.Empty<FolderAllocationListItemDto>(), 0);
        }

        var parameters = new DynamicParameters();
        parameters.Add("UnitScopeIds", unitIdsArray);

        var whereClause = "WHERE fua.IS_DELETED = 0 AND fua.UNIT_ID IN :UnitScopeIds";

        if (!string.IsNullOrEmpty(status))
        {
            whereClause += " AND fua.STATUS = :Status";
            parameters.Add("Status", status);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            whereClause += " AND (LOWER(f.NAME) LIKE :Keyword OR LOWER(u.FullName) LIKE :Keyword OR LOWER(u.UserName) LIKE :Keyword)";
            parameters.Add("Keyword", $"%{keyword.ToLower()}%");
        }

        if (fromDate.HasValue)
        {
            whereClause += " AND fua.CREATED_DATE >= :FromDate";
            parameters.Add("FromDate", fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            whereClause += " AND fua.CREATED_DATE < :ToDateExclusive";
            parameters.Add("ToDateExclusive", toDate.Value.Date.AddDays(1));
        }

        var countSql = $@"
            SELECT COUNT(*) 
            FROM FOLDER_USER_ALLOCATIONS fua
            INNER JOIN FOLDERS f ON f.ID = fua.FOLDER_ID
            INNER JOIN APP_USER u ON u.Id = fua.USER_ID
            {whereClause}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        if (totalCount == 0)
        {
            return (Enumerable.Empty<FolderAllocationListItemDto>(), 0);
        }

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var sql = $@"
            WITH FolderPaths AS (
                SELECT ID, NAME, SUBSTR(SYS_CONNECT_BY_PATH(NAME, ' / '), 4) AS Path
                FROM FOLDERS
                START WITH PARENT_ID IS NULL AND IS_DELETED = 0
                CONNECT BY PRIOR ID = PARENT_ID AND IS_DELETED = 0
            )
            SELECT 
                fua.ID AS Id,
                fua.FOLDER_ID AS FolderId,
                f.NAME AS FolderName,
                fp.Path AS FolderPath,
                fua.USER_ID AS UserId,
                u.UserName AS UserName,
                u.FullName AS UserFullName,
                fua.CREATED_DATE AS AllocatedDate,
                fua.STATUS AS Status,
                fua.UNIT_ID AS UnitId,
                ou.NAME AS UnitName
            FROM FOLDER_USER_ALLOCATIONS fua
            INNER JOIN FOLDERS f ON f.ID = fua.FOLDER_ID
            LEFT JOIN FolderPaths fp ON fp.ID = fua.FOLDER_ID
            INNER JOIN APP_USER u ON u.Id = fua.USER_ID
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = fua.UNIT_ID
            {whereClause}
            ORDER BY fua.CREATED_DATE DESC
            OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        var items = await _connection.QueryAsync<FolderAllocationListItemDto>(sql, parameters);
        return (items, totalCount);
    }

    public async Task<FolderAllocationListItemDto?> GetByIdAsync(Guid id, IEnumerable<long> unitScopeIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            WITH FolderPaths AS (
                SELECT ID, NAME, SUBSTR(SYS_CONNECT_BY_PATH(NAME, ' / '), 4) AS Path
                FROM FOLDERS
                START WITH PARENT_ID IS NULL AND IS_DELETED = 0
                CONNECT BY PRIOR ID = PARENT_ID AND IS_DELETED = 0
            )
            SELECT 
                fua.ID AS Id,
                fua.FOLDER_ID AS FolderId,
                f.NAME AS FolderName,
                fp.Path AS FolderPath,
                fua.USER_ID AS UserId,
                u.UserName AS UserName,
                u.FullName AS UserFullName,
                fua.CREATED_DATE AS AllocatedDate,
                fua.STATUS AS Status,
                fua.UNIT_ID AS UnitId,
                ou.NAME AS UnitName
            FROM FOLDER_USER_ALLOCATIONS fua
            INNER JOIN FOLDERS f ON f.ID = fua.FOLDER_ID
            LEFT JOIN FolderPaths fp ON fp.ID = fua.FOLDER_ID
            INNER JOIN APP_USER u ON u.Id = fua.USER_ID
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = fua.UNIT_ID
            WHERE fua.ID = :Id AND fua.IS_DELETED = 0 AND fua.UNIT_ID IN :UnitScopeIds";

        return await _connection.QuerySingleOrDefaultAsync<FolderAllocationListItemDto>(sql, new { Id = id.ToString(), UnitScopeIds = unitScopeIds.ToArray() });
    }

    public async Task<FolderUserAllocation?> GetEntityByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                ID, FOLDER_ID AS FolderId, USER_ID AS UserId, UNIT_ID AS UnitId,
                STATUS, ROW_VERSION AS RowVersion, CREATED_BY AS CreatedBy,
                CREATED_DATE AS CreatedDate, MODIFIED_BY AS ModifiedBy,
                MODIFIED_DATE AS ModifiedDate, IS_DELETED AS IsDeleted
            FROM FOLDER_USER_ALLOCATIONS
            WHERE ID = :Id AND IS_DELETED = 0";

        return await _connection.QuerySingleOrDefaultAsync<FolderUserAllocation>(sql, new { Id = id.ToString() });
    }

    public async Task<Guid> CreateAsync(FolderUserAllocation entity)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            INSERT INTO FOLDER_USER_ALLOCATIONS (
                ID, FOLDER_ID, USER_ID, UNIT_ID, STATUS, ROW_VERSION,
                CREATED_BY, CREATED_DATE, IS_DELETED
            ) VALUES (
                :Id, :FolderId, :UserId, :UnitId, :Status, 1,
                :CreatedBy, SYSTIMESTAMP, 0
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = entity.Id.ToString(),
            FolderId = entity.FolderId.ToString(),
            entity.UserId,
            entity.UnitId,
            entity.Status,
            entity.CreatedBy
        });

        return entity.Id;
    }

    public async Task<bool> UpdateAsync(FolderUserAllocation entity)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            UPDATE FOLDER_USER_ALLOCATIONS
            SET 
                FOLDER_ID = :FolderId,
                USER_ID = :UserId,
                STATUS = :Status,
                ROW_VERSION = ROW_VERSION + 1,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP,
                IS_DELETED = :IsDeleted
            WHERE ID = :Id AND ROW_VERSION = :ExpectedVersion";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            FolderId = entity.FolderId.ToString(),
            entity.UserId,
            entity.Status,
            entity.ModifiedBy,
            IsDeleted = entity.IsDeleted ? 1 : 0,
            Id = entity.Id.ToString(),
            ExpectedVersion = entity.RowVersion
        });

        return affected > 0;
    }

    public async Task<IEnumerable<FolderUserAllocation>> GetActiveAllocationsByUserAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                ID, FOLDER_ID AS FolderId, USER_ID AS UserId, UNIT_ID AS UnitId,
                STATUS, ROW_VERSION AS RowVersion, CREATED_BY AS CreatedBy,
                CREATED_DATE AS CreatedDate, MODIFIED_BY AS ModifiedBy,
                MODIFIED_DATE AS ModifiedDate, IS_DELETED AS IsDeleted
            FROM FOLDER_USER_ALLOCATIONS
            WHERE USER_ID = :UserId AND STATUS = 'Active' AND IS_DELETED = 0";

        return await _connection.QueryAsync<FolderUserAllocation>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<UserLookupItemDto>> GetUsersInUnitScopeAsync(IEnumerable<long> unitScopeIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                u.Id AS Id,
                u.UserName AS UserName,
                u.FullName AS FullName,
                u.OrganizationUnitId AS OrganizationUnitId,
                ou.NAME AS OrganizationUnitName
            FROM APP_USER u
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = u.OrganizationUnitId
            WHERE u.IsActive = 1 AND u.OrganizationUnitId IN :UnitScopeIds
            ORDER BY u.FullName ASC";

        return await _connection.QueryAsync<UserLookupItemDto>(sql, new { UnitScopeIds = unitScopeIds.ToArray() });
    }

    public async Task<IEnumerable<FolderLookupItemDto>> GetFoldersInUnitScopeAsync(IEnumerable<long> unitScopeIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT 
                f.ID AS Id,
                f.NAME AS Name,
                f.PARENT_ID AS ParentId,
                f.UNIT_ID AS UnitId,
                ou.CODE AS UnitCode
            FROM FOLDERS f
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = f.UNIT_ID
            WHERE f.IS_DELETED = 0 AND f.UNIT_ID IN :UnitScopeIds
            ORDER BY f.NAME ASC";

        return await _connection.QueryAsync<FolderLookupItemDto>(sql, new { UnitScopeIds = unitScopeIds.ToArray() });
    }

    public async Task<IEnumerable<FolderLookupItemDto>> GetMyAllocatedFoldersAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT DISTINCT
                f.ID AS Id,
                f.NAME AS Name,
                f.PARENT_ID AS ParentId,
                f.UNIT_ID AS UnitId,
                ou.CODE AS UnitCode
            FROM FOLDER_USER_ALLOCATIONS fua
            INNER JOIN FOLDERS f ON f.ID = fua.FOLDER_ID
            INNER JOIN ORGANIZATION_UNIT ou ON ou.ID = f.UNIT_ID
            WHERE fua.USER_ID = :UserId AND fua.STATUS = 'Active' AND fua.IS_DELETED = 0 AND f.IS_DELETED = 0
            ORDER BY f.NAME ASC";

        return await _connection.QueryAsync<FolderLookupItemDto>(sql, new { UserId = userId });
    }

    public async Task<bool> IsUserAdminAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"
            SELECT COUNT(*) 
            FROM APP_USER u
            LEFT JOIN USER_ROLE ur ON ur.UserId = u.Id
            LEFT JOIN ROLE r ON r.Id = ur.RoleId
            WHERE u.Id = :UserId 
              AND (LOWER(u.UserName) = 'admin' OR r.Code = 'ADMIN' OR r.Code = 'SUPER_ADMIN')";

        var count = await _connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
        return count > 0;
    }
}
