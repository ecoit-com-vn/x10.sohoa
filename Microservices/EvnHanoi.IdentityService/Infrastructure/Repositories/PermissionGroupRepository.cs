using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class PermissionGroupRepository : IPermissionGroupRepository
{
    private readonly IDbConnection _connection;
    private readonly IPermissionRepository _permissionRepository;

    public PermissionGroupRepository(IDbConnection connection, IPermissionRepository permissionRepository)
    {
        _connection = connection;
        _permissionRepository = permissionRepository;
    }

    public async Task<IEnumerable<PermissionGroup>> GetAllAsync(string groupType, long? organizationUnitId = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = BuildSelectSql("WHERE st.Code = :GroupType");
        var parameters = new DynamicParameters();
        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;
        parameters.Add("GroupType", dbGroupType);

        if (organizationUnitId.HasValue)
        {
            sql += " AND pg.OrganizationUnitId = :OrganizationUnitId";
            parameters.Add("OrganizationUnitId", organizationUnitId.Value);
        }

        sql += " ORDER BY pg.Id";
        return await _connection.QueryAsync<PermissionGroup>(sql, parameters);
    }

    public async Task<(IEnumerable<PermissionGroup> Items, int TotalCount)> GetPagedAsync(
        string groupType, int page, int pageSize, string? keyword = null, long? organizationUnitId = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var conditions = new List<string> { "st.Code = :GroupType" };
        var parameters = new DynamicParameters();
        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;
        parameters.Add("GroupType", dbGroupType);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            conditions.Add("(UPPER(pg.Code) LIKE UPPER(:Keyword) OR UPPER(pg.Name) LIKE UPPER(:Keyword) OR UPPER(pg.Description) LIKE UPPER(:Keyword))");
            parameters.Add("Keyword", $"%{keyword.Trim()}%");
        }

        if (organizationUnitId.HasValue)
        {
            conditions.Add("pg.OrganizationUnitId = :OrganizationUnitId");
            parameters.Add("OrganizationUnitId", organizationUnitId.Value);
        }

        var whereClause = "WHERE " + string.Join(" AND ", conditions);
        var countSql = $"SELECT COUNT(*) FROM PERMISSION_GROUP pg INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id {whereClause}";
        var offset = (page - 1) * pageSize;

        var sql = $@"
            SELECT * FROM (
                SELECT pg.Id, pg.Code, pg.Name, pg.Description, pg.ScopeTypeId,
                       CASE WHEN st.Code = 'GLOBAL' THEN 'SYSTEM' ELSE st.Code END AS GroupType,
                       st.Name AS ScopeTypeName, pg.OrganizationUnitId,
                       o.Name AS OrganizationUnitName, pg.IsActive,
                       ROW_NUMBER() OVER (ORDER BY pg.Id ASC) AS RN
                FROM PERMISSION_GROUP pg
                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
                LEFT JOIN ORGANIZATION_UNIT o ON pg.OrganizationUnitId = o.Id
                {whereClause}
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";

        parameters.Add("Offset", offset);
        parameters.Add("OffsetPlusSize", offset + pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await _connection.QueryAsync<PermissionGroup>(sql, parameters);
        return (items, totalCount);
    }

    public async Task<PermissionGroup?> GetByIdAsync(long id, string groupType)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = BuildSelectSql("WHERE pg.Id = :Id AND st.Code = :GroupType");
        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;
        return await _connection.QuerySingleOrDefaultAsync<PermissionGroup>(sql, new { Id = id, GroupType = dbGroupType });
    }

    public async Task<long> CreateAsync(PermissionGroup group)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"
            INSERT INTO PERMISSION_GROUP (Code, Name, Description, ScopeTypeId, OrganizationUnitId, IsActive)
            VALUES (:Code, :Name, :Description, :ScopeTypeId, :OrganizationUnitId, :IsActive)
            RETURNING Id INTO :Id";

        var scopeTypeId = group.ScopeTypeId;
        if (scopeTypeId <= 0)
        {
            var code = string.Equals(group.GroupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : group.GroupType;
            scopeTypeId = string.Equals(code, "UNIT", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        }

        var parameters = new DynamicParameters();
        parameters.Add("Code", group.Code);
        parameters.Add("Name", group.Name);
        parameters.Add("Description", group.Description);
        parameters.Add("ScopeTypeId", scopeTypeId);
        parameters.Add("OrganizationUnitId", group.OrganizationUnitId);
        parameters.Add("IsActive", group.IsActive ? 1 : 0);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(PermissionGroup group)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"
            UPDATE PERMISSION_GROUP
            SET Code = :Code, Name = :Name, Description = :Description,
                OrganizationUnitId = :OrganizationUnitId, IsActive = :IsActive, UpdatedAt = CURRENT_TIMESTAMP
            WHERE Id = :Id AND ScopeTypeId = :ScopeTypeId";

        var scopeTypeId = group.ScopeTypeId;
        if (scopeTypeId <= 0)
        {
            var code = string.Equals(group.GroupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : group.GroupType;
            scopeTypeId = string.Equals(code, "UNIT", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        }

        var affected = await _connection.ExecuteAsync(sql, new
        {
            group.Code,
            group.Name,
            group.Description,
            group.OrganizationUnitId,
            IsActive = group.IsActive ? 1 : 0,
            group.Id,
            ScopeTypeId = scopeTypeId
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id, string groupType)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"
            DELETE FROM PERMISSION_GROUP 
            WHERE Id = :Id 
              AND ScopeTypeId = (SELECT Id FROM SCOPE_TYPE WHERE Code = :GroupType)";
        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;
        var affected = await _connection.ExecuteAsync(sql, new { Id = id, GroupType = dbGroupType });
        return affected > 0;
    }

    public async Task<IEnumerable<string>> GetPermissionCodesByGroupIdAsync(long permissionGroupId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = @"
            SELECT p.Code
            FROM PERMISSION_GROUP_PERMISSION pgp
            INNER JOIN PERMISSION p ON pgp.PermissionId = p.Id
            WHERE pgp.PermissionGroupId = :PermissionGroupId";
        return await _connection.QueryAsync<string>(sql, new { PermissionGroupId = permissionGroupId });
    }

    public async Task<bool> AssignPermissionsToGroupAsync(long permissionGroupId, IEnumerable<string> permissionCodes)
    {
        var permissions = await _permissionRepository.GetAllPermissionsAsync();
        var codeToIdMap = permissions
            .Where(p => p.IsActive)
            .ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);

        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteAsync(
                "DELETE FROM PERMISSION_GROUP_PERMISSION WHERE PermissionGroupId = :PermissionGroupId",
                new { PermissionGroupId = permissionGroupId },
                transaction);

            const string sql = @"
                INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
                VALUES (:Id, :PermissionGroupId, :PermissionId)";

            foreach (var code in permissionCodes)
            {
                if (codeToIdMap.TryGetValue(code, out var permissionId))
                {
                    await _connection.ExecuteAsync(sql, new
                    {
                        Id = Guid.NewGuid().ToString(),
                        PermissionGroupId = permissionGroupId,
                        PermissionId = permissionId
                    }, transaction);
                }
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<long>> GetPermissionGroupIdsByRoleIdAsync(long roleId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = "SELECT PermissionGroupId FROM ROLE_PERMISSION_GROUP WHERE RoleId = :RoleId";
        return await _connection.QueryAsync<long>(sql, new { RoleId = roleId });
    }

    public async Task<IEnumerable<PermissionGroup>> GetPermissionGroupsByRoleIdAsync(long roleId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = @"
            SELECT pg.Id, pg.Code, pg.Name, pg.Description, pg.ScopeTypeId,
                   CASE WHEN st.Code = 'GLOBAL' THEN 'SYSTEM' ELSE st.Code END AS GroupType,
                   st.Name AS ScopeTypeName, pg.OrganizationUnitId,
                   o.Name AS OrganizationUnitName, pg.IsActive
            FROM ROLE_PERMISSION_GROUP rpg
            INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id
            INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
            LEFT JOIN ORGANIZATION_UNIT o ON pg.OrganizationUnitId = o.Id
            WHERE rpg.RoleId = :RoleId
            ORDER BY st.Code, pg.Name";
        return await _connection.QueryAsync<PermissionGroup>(sql, new { RoleId = roleId });
    }

    private static string BuildSelectSql(string whereClause) => $@"
        SELECT pg.Id, pg.Code, pg.Name, pg.Description, pg.ScopeTypeId,
               CASE WHEN st.Code = 'GLOBAL' THEN 'SYSTEM' ELSE st.Code END AS GroupType,
               st.Name AS ScopeTypeName, pg.OrganizationUnitId,
               o.Name AS OrganizationUnitName, pg.IsActive
        FROM PERMISSION_GROUP pg
        INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
        LEFT JOIN ORGANIZATION_UNIT o ON pg.OrganizationUnitId = o.Id
        {whereClause}";
}
