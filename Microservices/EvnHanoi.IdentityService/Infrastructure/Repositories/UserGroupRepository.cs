// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Infrastructure\Repositories\UserGroupRepository.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UserGroupRepository : IUserGroupRepository
{
    private readonly string _connectionString;

    public UserGroupRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<UserGroup>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Name, Description, IsActive FROM USER_GROUP ORDER BY Id";
        return await connection.QueryAsync<UserGroup>(sql);
    }

    public async Task<UserGroup?> GetByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Name, Description, IsActive FROM USER_GROUP WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<UserGroup>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(UserGroup group)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO USER_GROUP (Name, Description, IsActive)
            VALUES (:Name, :Description, :IsActive)
            RETURNING Id INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Name", group.Name);
        parameters.Add("Description", group.Description);
        parameters.Add("IsActive", group.IsActive ? 1 : 0);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(UserGroup group)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE USER_GROUP 
            SET Name = :Name, 
                Description = :Description, 
                IsActive = :IsActive 
            WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new 
        {
            group.Name,
            group.Description,
            IsActive = group.IsActive ? 1 : 0,
            group.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM USER_GROUP WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<User>> GetMembersAsync(long groupId)
    {
        using var connection = CreateConnection();
        var sql = @"
            SELECT u.Id, u.UserName AS Username, u.Email, u.FullName, u.IsActive, u.OrganizationUnitId AS UnitId 
            FROM APP_USER u
            INNER JOIN USER_GROUP_MEMBER ugm ON u.Id = ugm.UserId
            WHERE ugm.UserGroupId = :GroupId";
        return await connection.QueryAsync<User>(sql, new { GroupId = groupId });
    }

    public async Task<bool> AssignMembersAsync(long groupId, IEnumerable<long> userIds)
    {
        using var connection = CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Xóa danh sách thành viên cũ
            await connection.ExecuteAsync(
                "DELETE FROM USER_GROUP_MEMBER WHERE UserGroupId = :GroupId", 
                new { GroupId = groupId }, 
                transaction);

            // Thêm mới danh sách thành viên
            var sql = "INSERT INTO USER_GROUP_MEMBER (UserGroupId, UserId) VALUES (:GroupId, :UserId)";
            foreach (var userId in userIds)
            {
                await connection.ExecuteAsync(sql, new { GroupId = groupId, UserId = userId }, transaction);
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

    public async Task<IEnumerable<Role>> GetRolesAsync(long groupId)
    {
        using var connection = CreateConnection();
        var sql = @"
            SELECT r.Id, r.Code, r.Name, r.Description
            FROM ROLE r
            INNER JOIN USER_GROUP_ROLE ugr ON r.Id = ugr.RoleId
            WHERE ugr.UserGroupId = :GroupId";
        return await connection.QueryAsync<Role>(sql, new { GroupId = groupId });
    }

    public async Task<bool> AssignRolesAsync(long groupId, IEnumerable<long> roleIds)
    {
        using var connection = CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Xóa vai trò cũ của nhóm
            await connection.ExecuteAsync(
                "DELETE FROM USER_GROUP_ROLE WHERE UserGroupId = :GroupId", 
                new { GroupId = groupId }, 
                transaction);

            // Thêm mới vai trò
            var sql = "INSERT INTO USER_GROUP_ROLE (UserGroupId, RoleId) VALUES (:GroupId, :RoleId)";
            foreach (var roleId in roleIds)
            {
                await connection.ExecuteAsync(sql, new { GroupId = groupId, RoleId = roleId }, transaction);
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
