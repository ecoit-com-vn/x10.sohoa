using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UserGroupRepository : IUserGroupRepository
{
    private readonly IDbConnection _connection;

    public UserGroupRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<UserGroup>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserGroup.Id)}, 
                   {nameof(UserGroup.Name)}, 
                   {nameof(UserGroup.Description)}, 
                   {nameof(UserGroup.IsActive)} 
            FROM USER_GROUP 
            ORDER BY {nameof(UserGroup.Id)}";
        return await _connection.QueryAsync<UserGroup>(sql);
    }

    public async Task<UserGroup?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserGroup.Id)}, 
                   {nameof(UserGroup.Name)}, 
                   {nameof(UserGroup.Description)}, 
                   {nameof(UserGroup.IsActive)} 
            FROM USER_GROUP 
            WHERE {nameof(UserGroup.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<UserGroup>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(UserGroup group)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            INSERT INTO USER_GROUP (
                {nameof(UserGroup.Name)}, 
                {nameof(UserGroup.Description)}, 
                {nameof(UserGroup.IsActive)}
            )
            VALUES (:Name, :Description, :IsActive)
            RETURNING {nameof(UserGroup.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Name", group.Name);
        parameters.Add("Description", group.Description);
        parameters.Add("IsActive", group.IsActive ? 1 : 0);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(UserGroup group)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE USER_GROUP 
            SET {nameof(UserGroup.Name)} = :Name, 
                {nameof(UserGroup.Description)} = :Description, 
                {nameof(UserGroup.IsActive)} = :IsActive 
            WHERE {nameof(UserGroup.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new 
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
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $"DELETE FROM USER_GROUP WHERE {nameof(UserGroup.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<User>> GetMembersAsync(long groupId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT u.{nameof(User.Id)}, 
                   u.UserName AS {nameof(User.Username)}, 
                   u.{nameof(User.Email)}, 
                   u.{nameof(User.FullName)}, 
                   u.{nameof(User.IsActive)}, 
                   u.{nameof(User.OrganizationUnitId)},
                   o.{nameof(OrganizationUnit.Id)}, 
                   o.{nameof(OrganizationUnit.Code)}, 
                   o.{nameof(OrganizationUnit.Name)}, 
                   o.{nameof(OrganizationUnit.ParentId)}, 
                   o.{nameof(OrganizationUnit.Description)}
            FROM APP_USER u
            INNER JOIN USER_GROUP_MEMBER ugm ON u.Id = ugm.UserId
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(User.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)}
            WHERE ugm.UserGroupId = :GroupId";
            
        return await _connection.QueryAsync<User, OrganizationUnit, User>(
            sql, 
            (user, unit) => {
                user.OrganizationUnit = unit;
                return user;
            },
            new { GroupId = groupId },
            splitOn: "Id"
        );
    }

    public async Task<bool> AssignMembersAsync(long groupId, IEnumerable<string> userIds)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            // Xóa danh sách thành viên cũ
            await _connection.ExecuteAsync(
                "DELETE FROM USER_GROUP_MEMBER WHERE UserGroupId = :GroupId", 
                new { GroupId = groupId }, 
                transaction);

            // Thêm mới danh sách thành viên
            var sql = "INSERT INTO USER_GROUP_MEMBER (UserGroupId, UserId) VALUES (:GroupId, :UserId)";
            foreach (var userId in userIds)
            {
                await _connection.ExecuteAsync(sql, new { GroupId = groupId, UserId = userId }, transaction);
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
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT r.{nameof(Role.Id)}, 
                   r.{nameof(Role.Code)}, 
                   r.{nameof(Role.Name)}, 
                   r.{nameof(Role.Description)}
            FROM ROLE r
            INNER JOIN USER_GROUP_ROLE ugr ON r.Id = ugr.RoleId
            WHERE ugr.UserGroupId = :GroupId";
        return await _connection.QueryAsync<Role>(sql, new { GroupId = groupId });
    }

    public async Task<bool> AssignRolesAsync(long groupId, IEnumerable<long> roleIds)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            // Xóa vai trò cũ của nhóm
            await _connection.ExecuteAsync(
                "DELETE FROM USER_GROUP_ROLE WHERE UserGroupId = :GroupId", 
                new { GroupId = groupId }, 
                transaction);

            // Thêm mới vai trò
            var sql = "INSERT INTO USER_GROUP_ROLE (UserGroupId, RoleId) VALUES (:GroupId, :RoleId)";
            foreach (var roleId in roleIds)
            {
                await _connection.ExecuteAsync(sql, new { GroupId = groupId, RoleId = roleId }, transaction);
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
