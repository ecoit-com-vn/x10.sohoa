using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly IDbConnection _connection;

    public RoleRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<Role>> GetAllAsync(int? scopeTypeId = null, long? organizationUnitId = null, bool includeDescendants = false)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var (whereClause, parameters) = BuildFilter(scopeTypeId, organizationUnitId, includeDescendants, keyword: null);
        var sql = $@"
            SELECT r.Id, r.Code, r.Name, r.Description, r.ScopeTypeId, st.Code AS ScopeType, st.Name AS ScopeTypeName, r.OrganizationUnitId,
                   o.Name AS OrganizationUnitName, r.IsActive
            FROM ROLE r
            INNER JOIN SCOPE_TYPE st ON r.ScopeTypeId = st.Id
            LEFT JOIN ORGANIZATION_UNIT o ON r.OrganizationUnitId = o.Id
            {whereClause}
            ORDER BY r.Id";
        return await _connection.QueryAsync<Role>(sql, parameters);
    }

    public async Task<(IEnumerable<Role> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? keyword = null, int? scopeTypeId = null,
        long? organizationUnitId = null, bool includeDescendants = false)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var (whereClause, parameters) = BuildFilter(scopeTypeId, organizationUnitId, includeDescendants, keyword);
        var countSql = $"SELECT COUNT(*) FROM ROLE r INNER JOIN SCOPE_TYPE st ON r.ScopeTypeId = st.Id {whereClause}";
        var offset = (page - 1) * pageSize;

        var sql = $@"
            SELECT * FROM (
                SELECT r.Id, r.Code, r.Name, r.Description, r.ScopeTypeId, st.Code AS ScopeType, st.Name AS ScopeTypeName, r.OrganizationUnitId,
                       o.Name AS OrganizationUnitName, r.IsActive,
                       ROW_NUMBER() OVER (ORDER BY r.Id ASC) AS RN
                FROM ROLE r
                INNER JOIN SCOPE_TYPE st ON r.ScopeTypeId = st.Id
                LEFT JOIN ORGANIZATION_UNIT o ON r.OrganizationUnitId = o.Id
                {whereClause}
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";

        parameters.Add("Offset", offset);
        parameters.Add("OffsetPlusSize", offset + pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await _connection.QueryAsync<Role>(sql, parameters);
        return (items, totalCount);
    }

    public async Task<Role?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = @"
            SELECT r.Id, r.Code, r.Name, r.Description, r.ScopeTypeId, st.Code AS ScopeType, st.Name AS ScopeTypeName, r.OrganizationUnitId,
                   o.Name AS OrganizationUnitName, r.IsActive
            FROM ROLE r
            INNER JOIN SCOPE_TYPE st ON r.ScopeTypeId = st.Id
            LEFT JOIN ORGANIZATION_UNIT o ON r.OrganizationUnitId = o.Id
            WHERE r.Id = :Id";
        return await _connection.QuerySingleOrDefaultAsync<Role>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Role role)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = @"
            INSERT INTO ROLE (Code, Name, Description, ScopeTypeId, OrganizationUnitId, IsActive)
            VALUES (:Code, :Name, :Description, :ScopeTypeId, :OrganizationUnitId, :IsActive)
            RETURNING Id INTO :Id";

        var parameters = new DynamicParameters();
        parameters.Add("Code", role.Code);
        parameters.Add("Name", role.Name);
        parameters.Add("Description", role.Description);
        parameters.Add("ScopeTypeId", role.ScopeTypeId);
        parameters.Add("OrganizationUnitId", role.OrganizationUnitId);
        parameters.Add("IsActive", role.IsActive ? 1 : 0);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Role role)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = @"
            UPDATE ROLE
            SET Code = :Code, Name = :Name, Description = :Description,
                ScopeTypeId = :ScopeTypeId, OrganizationUnitId = :OrganizationUnitId,
                IsActive = :IsActive, UpdatedAt = CURRENT_TIMESTAMP
            WHERE Id = :Id";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            role.Code,
            role.Name,
            role.Description,
            role.ScopeTypeId,
            role.OrganizationUnitId,
            IsActive = role.IsActive ? 1 : 0,
            role.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = "DELETE FROM ROLE WHERE Id = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<bool> AssignPermissionGroupsAsync(long roleId, IEnumerable<long> permissionGroupIds)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteAsync(
                "DELETE FROM ROLE_PERMISSION_GROUP WHERE RoleId = :RoleId",
                new { RoleId = roleId },
                transaction);

            const string sql = @"
                INSERT INTO ROLE_PERMISSION_GROUP (RoleId, PermissionGroupId)
                VALUES (:RoleId, :PermissionGroupId)";

            foreach (var groupId in permissionGroupIds.Distinct())
            {
                await _connection.ExecuteAsync(sql, new { RoleId = roleId, PermissionGroupId = groupId }, transaction);
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

    public async Task<(IEnumerable<RoleAssignedUserListItem> Items, int TotalCount)> GetUsersByRoleIdPagedAsync(
        long roleId, int page, int pageSize, string? keyword = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var parameters = new DynamicParameters();
        parameters.Add("RoleId", roleId);

        var keywordFilter = "";
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keywordFilter = @" AND (
                UPPER(u.UserName) LIKE UPPER(:Keyword)
                OR UPPER(u.FullName) LIKE UPPER(:Keyword)
                OR UPPER(u.Email) LIKE UPPER(:Keyword)
            )";
            parameters.Add("Keyword", $"%{keyword.Trim()}%");
        }

        var baseFrom = $@"
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.OrganizationUnitId = o.Id
            WHERE u.IsDeleted = 0 AND (
                EXISTS (SELECT 1 FROM USER_ROLE ur WHERE ur.UserId = u.Id AND ur.RoleId = :RoleId)
                OR EXISTS (
                    SELECT 1 FROM USER_GROUP_MEMBER ugm
                    INNER JOIN USER_GROUP_ROLE ugr ON ugm.UserGroupId = ugr.UserGroupId
                    WHERE ugm.UserId = u.Id AND ugr.RoleId = :RoleId
                )
            ){keywordFilter}";

        var countSql = $"SELECT COUNT(DISTINCT u.Id) {baseFrom}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Skip", offset);
        parameters.Add("Take", pageSize);

        var sql = $@"
            SELECT DISTINCT
                u.Id AS {nameof(RoleAssignedUserListItem.Id)},
                u.UserName AS {nameof(RoleAssignedUserListItem.Username)},
                u.FullName AS {nameof(RoleAssignedUserListItem.FullName)},
                u.Email AS {nameof(RoleAssignedUserListItem.Email)},
                u.IsActive AS {nameof(RoleAssignedUserListItem.IsActive)},
                o.Name AS {nameof(RoleAssignedUserListItem.OrganizationUnitName)}
            {baseFrom}
            ORDER BY u.FullName, u.UserName
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY";

        var items = await _connection.QueryAsync<RoleAssignedUserListItem>(sql, parameters);
        return (items, totalCount);
    }

    private static (string WhereClause, DynamicParameters Parameters) BuildFilter(
        int? scopeTypeId, long? organizationUnitId, bool includeDescendants, string? keyword)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (scopeTypeId.HasValue)
        {
            conditions.Add("st.Id = :ScopeTypeId");
            parameters.Add("ScopeTypeId", scopeTypeId);
        }

        if (organizationUnitId.HasValue)
        {
            if (includeDescendants)
            {
                conditions.Add(@"r.OrganizationUnitId IN (
                    SELECT Id FROM ORGANIZATION_UNIT
                    WHERE IsDeleted = 0
                    START WITH Id = :OrganizationUnitId
                    CONNECT BY PRIOR Id = ParentId)");
            }
            else
            {
                conditions.Add("r.OrganizationUnitId = :OrganizationUnitId");
            }

            parameters.Add("OrganizationUnitId", organizationUnitId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            conditions.Add("(UPPER(r.Code) LIKE UPPER(:Keyword) OR UPPER(r.Name) LIKE UPPER(:Keyword) OR UPPER(r.Description) LIKE UPPER(:Keyword))");
            parameters.Add("Keyword", $"%{keyword.Trim()}%");
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        return (whereClause, parameters);
    }
}
