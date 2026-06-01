using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly string _connectionString;

    public RoleRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<Role>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Code, Name, Description FROM ROLE ORDER BY Id";
        return await connection.QueryAsync<Role>(sql);
    }

    public async Task<Role?> GetByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Code, Name, Description FROM ROLE WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<Role>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Role role)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO ROLE (Code, Name, Description)
            VALUES (:Code, :Name, :Description)
            RETURNING Id INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Code", role.Code);
        parameters.Add("Name", role.Name);
        parameters.Add("Description", role.Description);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Role role)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE ROLE 
            SET Code = :Code, 
                Name = :Name, 
                Description = :Description 
            WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new 
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
        using var connection = CreateConnection();
        var sql = "DELETE FROM ROLE WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<string>> GetPermissionsByRoleIdAsync(long roleId)
    {
        using var connection = CreateConnection();
        var sql = "SELECT PermissionCode FROM ROLE_PERMISSION WHERE RoleId = :RoleId";
        return await connection.QueryAsync<string>(sql, new { RoleId = roleId });
    }

    public async Task<bool> AssignPermissionsToRoleAsync(long roleId, IEnumerable<string> permissionCodes)
    {
        using var connection = CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Clear existing permissions
            await connection.ExecuteAsync(
                "DELETE FROM ROLE_PERMISSION WHERE RoleId = :RoleId", 
                new { RoleId = roleId }, 
                transaction);

            // Insert new permissions
            var sql = "INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode) VALUES (:Id, :RoleId, :PermissionCode)";
            foreach (var code in permissionCodes)
            {
                var id = Guid.NewGuid().ToString(); // UUID
                await connection.ExecuteAsync(sql, new 
                {
                    Id = id,
                    RoleId = roleId,
                    PermissionCode = code
                }, transaction);
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
