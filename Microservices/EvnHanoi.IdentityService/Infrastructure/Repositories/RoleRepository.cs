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
    private readonly IPermissionRepository _permissionRepository;

    public RoleRepository(IDbConnection connection, IPermissionRepository permissionRepository)
    {
        _connection = connection;
        _permissionRepository = permissionRepository;
    }

    public async Task<IEnumerable<Role>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(Role.Id)}, 
                   {nameof(Role.Code)}, 
                   {nameof(Role.Name)}, 
                   {nameof(Role.Description)},
                   {nameof(Role.IsActive)}
            FROM ROLE 
            ORDER BY {nameof(Role.Id)}";
        return await _connection.QueryAsync<Role>(sql);
    }

    public async Task<(IEnumerable<Role> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        
        var whereClause = "";
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            whereClause = "WHERE (UPPER(r.Code) LIKE UPPER(:Keyword) OR UPPER(r.Name) LIKE UPPER(:Keyword) OR UPPER(r.Description) LIKE UPPER(:Keyword))";
            parameters.Add("Keyword", $"%{keyword}%");
        }
        
        var countSql = $"SELECT COUNT(*) FROM ROLE r {whereClause}";
        var offset = (page - 1) * pageSize;
        
        var sql = $@"
            SELECT * FROM (
                SELECT r.{nameof(Role.Id)}, 
                       r.{nameof(Role.Code)}, 
                       r.{nameof(Role.Name)}, 
                       r.{nameof(Role.Description)},
                       r.{nameof(Role.IsActive)},
                       ROW_NUMBER() OVER (ORDER BY r.{nameof(Role.Id)} ASC) AS RN
                FROM ROLE r
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
        var sql = $@"
            SELECT {nameof(Role.Id)}, 
                   {nameof(Role.Code)}, 
                   {nameof(Role.Name)}, 
                   {nameof(Role.Description)},
                   {nameof(Role.IsActive)}
            FROM ROLE 
            WHERE {nameof(Role.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<Role>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Role role)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            INSERT INTO ROLE (
                {nameof(Role.Code)}, 
                {nameof(Role.Name)}, 
                {nameof(Role.Description)},
                {nameof(Role.IsActive)}
            )
            VALUES (:Code, :Name, :Description, :IsActive)
            RETURNING {nameof(Role.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Code", role.Code);
        parameters.Add("Name", role.Name);
        parameters.Add("Description", role.Description);
        parameters.Add("IsActive", role.IsActive ? 1 : 0);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Role role)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE ROLE 
            SET {nameof(Role.Code)} = :Code, 
                {nameof(Role.Name)} = :Name, 
                {nameof(Role.Description)} = :Description,
                {nameof(Role.IsActive)} = :IsActive
            WHERE {nameof(Role.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new 
        {
            role.Code,
            role.Name,
            role.Description,
            IsActive = role.IsActive ? 1 : 0,
            role.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $"DELETE FROM ROLE WHERE {nameof(Role.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<string>> GetPermissionsByRoleIdAsync(long roleId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"
            SELECT p.Code 
            FROM ROLE_PERMISSION rp
            INNER JOIN PERMISSION p ON rp.PermissionId = p.Id
            WHERE rp.RoleId = :RoleId";
        return await _connection.QueryAsync<string>(sql, new { RoleId = roleId });
    }

    public async Task<bool> AssignPermissionsToRoleAsync(long roleId, IEnumerable<string> permissionCodes)
    {
        // Fetch active permissions to get ID from Code via IPermissionRepository
        var permissions = await _permissionRepository.GetAllPermissionsAsync();
        var codeToIdMap = permissions
            .Where(p => p.IsActive)
            .ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);

        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            // Clear existing permissions
            await _connection.ExecuteAsync(
                "DELETE FROM ROLE_PERMISSION WHERE RoleId = :RoleId", 
                new { RoleId = roleId }, 
                transaction);

            // Insert new permissions mapping
            var sql = @"
                INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId) 
                VALUES (:Id, :RoleId, :PermissionId)";
                
            foreach (var code in permissionCodes)
            {
                if (codeToIdMap.TryGetValue(code, out var permissionId))
                {
                    var id = Guid.NewGuid().ToString(); // UUID
                    await _connection.ExecuteAsync(sql, new 
                    {
                        Id = id,
                        RoleId = roleId,
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
            WHERE (
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
}
