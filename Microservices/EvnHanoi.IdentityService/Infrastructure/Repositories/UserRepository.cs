using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var connection = CreateConnection();
        var sql = @"
            SELECT Id, 
                   UserName AS Username, 
                   Email, 
                   FullName, 
                   PasswordHash, 
                   IsActive, 
                   OrganizationUnitId AS UnitId, 
                   AccessFailedCount, 
                   LockoutEnd, 
                   LockoutEnabled 
            FROM APP_USER 
            WHERE UserName = :Username";
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<System.Collections.Generic.IEnumerable<User>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = @"
            SELECT Id, 
                   UserName AS Username, 
                   Email, 
                   FullName, 
                   PasswordHash, 
                   IsActive, 
                   OrganizationUnitId AS UnitId, 
                   AccessFailedCount, 
                   LockoutEnd, 
                   LockoutEnabled 
            FROM APP_USER";
        return await connection.QueryAsync<User>(sql);
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = @"
            SELECT Id, 
                   UserName AS Username, 
                   Email, 
                   FullName, 
                   PasswordHash, 
                   IsActive, 
                   OrganizationUnitId AS UnitId, 
                   AccessFailedCount, 
                   LockoutEnd, 
                   LockoutEnabled 
            FROM APP_USER
            WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task UpdateAsync(User user)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE APP_USER 
            SET AccessFailedCount = :AccessFailedCount, 
                LockoutEnd = :LockoutEnd, 
                LockoutEnabled = :LockoutEnabled 
            WHERE Id = :Id";
        await connection.ExecuteAsync(sql, new 
        {
            user.AccessFailedCount,
            user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled ? 1 : 0,
            user.Id
        });
    }

    public async Task UpdateFullAsync(User user)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE APP_USER 
            SET UserName = :Username,
                Email = :Email,
                FullName = :FullName,
                PasswordHash = :PasswordHash,
                IsActive = :IsActive,
                OrganizationUnitId = :UnitId,
                LockoutEnabled = :LockoutEnabled
            WHERE Id = :Id";
        await connection.ExecuteAsync(sql, new 
        {
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PasswordHash = user.PasswordHash,
            IsActive = user.IsActive ? 1 : 0,
            UnitId = user.UnitId,
            LockoutEnabled = user.LockoutEnabled ? 1 : 0,
            Id = user.Id
        });
    }

    public async Task<long> CreateAsync(User user)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO APP_USER (UserName, Email, FullName, PasswordHash, IsActive, OrganizationUnitId)
            VALUES (:Username, :Email, :FullName, :PasswordHash, :IsActive, :UnitId)
            RETURNING Id INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Username", user.Username);
        parameters.Add("Email", user.Email);
        parameters.Add("FullName", user.FullName);
        parameters.Add("PasswordHash", user.PasswordHash);
        parameters.Add("IsActive", user.IsActive ? 1 : 0);
        parameters.Add("UnitId", user.UnitId);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task DeleteAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM APP_USER WHERE Id = :Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<System.Collections.Generic.IEnumerable<string>> GetRolesByUserIdAsync(long userId)
    {
        using var connection = CreateConnection();
        var sql = @"
            SELECT r.Code 
            FROM ROLE r
            WHERE r.Id IN (
                SELECT RoleId FROM USER_ROLE WHERE UserId = :UserId
                UNION
                SELECT ugr.RoleId 
                FROM USER_GROUP_MEMBER ugm
                INNER JOIN USER_GROUP_ROLE ugr ON ugm.UserGroupId = ugr.UserGroupId
                WHERE ugm.UserId = :UserId
            )";
        return await connection.QueryAsync<string>(sql, new { UserId = userId });
    }

    public async Task<System.Collections.Generic.IEnumerable<string>> GetPermissionsByUserIdAsync(long userId)
    {
        using var connection = CreateConnection();
        var sql = @"
            SELECT DISTINCT rp.PermissionCode
            FROM ROLE_PERMISSION rp
            WHERE rp.RoleId IN (
                SELECT RoleId FROM USER_ROLE WHERE UserId = :UserId
                UNION
                SELECT ugr.RoleId 
                FROM USER_GROUP_MEMBER ugm
                INNER JOIN USER_GROUP_ROLE ugr ON ugm.UserGroupId = ugr.UserGroupId
                WHERE ugm.UserId = :UserId
            )";
        return await connection.QueryAsync<string>(sql, new { UserId = userId });
    }
}
