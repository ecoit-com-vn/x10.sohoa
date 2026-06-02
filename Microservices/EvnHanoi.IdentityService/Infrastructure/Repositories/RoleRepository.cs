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

    public async Task<IEnumerable<Role>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(Role.Id)}, 
                   {nameof(Role.Code)}, 
                   {nameof(Role.Name)}, 
                   {nameof(Role.Description)} 
            FROM ROLE 
            ORDER BY {nameof(Role.Id)}";
        return await _connection.QueryAsync<Role>(sql);
    }

    public async Task<Role?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(Role.Id)}, 
                   {nameof(Role.Code)}, 
                   {nameof(Role.Name)}, 
                   {nameof(Role.Description)} 
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
                {nameof(Role.Description)}
            )
            VALUES (:Code, :Name, :Description)
            RETURNING {nameof(Role.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Code", role.Code);
        parameters.Add("Name", role.Name);
        parameters.Add("Description", role.Description);
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
                {nameof(Role.Description)} = :Description 
            WHERE {nameof(Role.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new 
        {
            role.Code,
            role.Name,
            role.Description,
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
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            // Clear existing permissions
            await _connection.ExecuteAsync(
                "DELETE FROM ROLE_PERMISSION WHERE RoleId = :RoleId", 
                new { RoleId = roleId }, 
                transaction);

            // Fetch active permissions to get ID from Code
            var permissions = await _connection.QueryAsync<Permission>(
                "SELECT Id, Code FROM PERMISSION WHERE IsActive = 1", 
                transaction: transaction);
            
            var codeToIdMap = permissions.ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);

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
}
