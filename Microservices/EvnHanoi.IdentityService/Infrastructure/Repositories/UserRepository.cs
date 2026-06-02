using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnection _connection;

    public UserRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT u.{nameof(User.Id)}, 
                   u.UserName AS {nameof(User.Username)}, 
                   u.{nameof(User.Email)}, 
                   u.{nameof(User.FullName)}, 
                   u.{nameof(User.PasswordHash)}, 
                   u.{nameof(User.IsActive)}, 
                   u.{nameof(User.OrganizationUnitId)}, 
                   u.{nameof(User.AccessFailedCount)}, 
                   u.{nameof(User.LockoutEnd)}, 
                   u.{nameof(User.LockoutEnabled)},
                   o.{nameof(OrganizationUnit.Id)}, 
                   o.{nameof(OrganizationUnit.Code)}, 
                   o.{nameof(OrganizationUnit.Name)}, 
                   o.{nameof(OrganizationUnit.ParentId)}, 
                   o.{nameof(OrganizationUnit.Description)}
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(User.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)}
            WHERE u.UserName = :Username";
            
        var result = await _connection.QueryAsync<User, OrganizationUnit, User>(
            sql, 
            (user, unit) => {
                user.OrganizationUnit = unit;
                return user;
            }, 
            new { Username = username },
            splitOn: "Id"
        );
        return result.FirstOrDefault();
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT u.{nameof(User.Id)}, 
                   u.UserName AS {nameof(User.Username)}, 
                   u.{nameof(User.Email)}, 
                   u.{nameof(User.FullName)}, 
                   u.{nameof(User.PasswordHash)}, 
                   u.{nameof(User.IsActive)}, 
                   u.{nameof(User.OrganizationUnitId)}, 
                   u.{nameof(User.AccessFailedCount)}, 
                   u.{nameof(User.LockoutEnd)}, 
                   u.{nameof(User.LockoutEnabled)},
                   o.{nameof(OrganizationUnit.Id)}, 
                   o.{nameof(OrganizationUnit.Code)}, 
                   o.{nameof(OrganizationUnit.Name)}, 
                   o.{nameof(OrganizationUnit.ParentId)}, 
                   o.{nameof(OrganizationUnit.Description)}
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(User.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)}";
            
        return await _connection.QueryAsync<User, OrganizationUnit, User>(
            sql, 
            (user, unit) => {
                user.OrganizationUnit = unit;
                return user;
            }, 
            splitOn: "Id"
        );
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT u.{nameof(User.Id)}, 
                   u.UserName AS {nameof(User.Username)}, 
                   u.{nameof(User.Email)}, 
                   u.{nameof(User.FullName)}, 
                   u.{nameof(User.PasswordHash)}, 
                   u.{nameof(User.IsActive)}, 
                   u.{nameof(User.OrganizationUnitId)}, 
                   u.{nameof(User.AccessFailedCount)}, 
                   u.{nameof(User.LockoutEnd)}, 
                   u.{nameof(User.LockoutEnabled)},
                   o.{nameof(OrganizationUnit.Id)}, 
                   o.{nameof(OrganizationUnit.Code)}, 
                   o.{nameof(OrganizationUnit.Name)}, 
                   o.{nameof(OrganizationUnit.ParentId)}, 
                   o.{nameof(OrganizationUnit.Description)}
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(User.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)}
            WHERE u.{nameof(User.Id)} = :Id";
            
        var result = await _connection.QueryAsync<User, OrganizationUnit, User>(
            sql, 
            (user, unit) => {
                user.OrganizationUnit = unit;
                return user;
            }, 
            new { Id = id },
            splitOn: "Id"
        );
        return result.FirstOrDefault();
    }

    public async Task UpdateAsync(User user)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE APP_USER 
            SET {nameof(User.AccessFailedCount)} = :AccessFailedCount, 
                {nameof(User.LockoutEnd)} = :LockoutEnd, 
                {nameof(User.LockoutEnabled)} = :LockoutEnabled 
            WHERE {nameof(User.Id)} = :Id";
            
        await _connection.ExecuteAsync(sql, new 
        {
            user.AccessFailedCount,
            user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled ? 1 : 0,
            user.Id
        });
    }

    public async Task UpdateFullAsync(User user)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE APP_USER 
            SET UserName = :Username,
                {nameof(User.Email)} = :Email,
                {nameof(User.FullName)} = :FullName,
                {nameof(User.PasswordHash)} = :PasswordHash,
                {nameof(User.IsActive)} = :IsActive,
                {nameof(User.OrganizationUnitId)} = :OrganizationUnitId,
                {nameof(User.LockoutEnabled)} = :LockoutEnabled
            WHERE {nameof(User.Id)} = :Id";
            
        await _connection.ExecuteAsync(sql, new 
        {
            Username = user.Username,
            user.Email,
            user.FullName,
            user.PasswordHash,
            IsActive = user.IsActive ? 1 : 0,
            user.OrganizationUnitId,
            LockoutEnabled = user.LockoutEnabled ? 1 : 0,
            user.Id
        });
    }

    public async Task<string> CreateAsync(User user)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        if (string.IsNullOrEmpty(user.Id))
        {
            user.Id = UuidHelper.NewUuid();
        }
        var sql = $@"
            INSERT INTO APP_USER (
                {nameof(User.Id)}, 
                UserName, 
                {nameof(User.Email)}, 
                {nameof(User.FullName)}, 
                {nameof(User.PasswordHash)}, 
                {nameof(User.IsActive)}, 
                {nameof(User.OrganizationUnitId)},
                {nameof(User.AccessFailedCount)},
                {nameof(User.LockoutEnabled)}
            )
            VALUES (:Id, :Username, :Email, :FullName, :PasswordHash, :IsActive, :OrganizationUnitId, :AccessFailedCount, :LockoutEnabled)";
            
        await _connection.ExecuteAsync(sql, new
        {
            user.Id,
            Username = user.Username,
            user.Email,
            user.FullName,
            user.PasswordHash,
            IsActive = user.IsActive ? 1 : 0,
            user.OrganizationUnitId,
            user.AccessFailedCount,
            LockoutEnabled = user.LockoutEnabled ? 1 : 0
        });
        return user.Id;
    }

    public async Task DeleteAsync(string id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $"DELETE FROM APP_USER WHERE {nameof(User.Id)} = :Id";
        await _connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<IEnumerable<string>> GetRolesByUserIdAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
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
        return await _connection.QueryAsync<string>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<string>> GetPermissionsByUserIdAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"
            SELECT DISTINCT p.Code
            FROM PERMISSION p
            WHERE p.IsActive = 1 AND p.Id IN (
                -- 1. Quyền gán trực tiếp cho User
                SELECT PermissionId FROM USER_PERMISSION WHERE UserId = :UserId
                
                UNION
                
                -- 2. Quyền gán qua Nhóm người dùng (User Group)
                SELECT ugp.PermissionId 
                FROM USER_GROUP_PERMISSION ugp
                INNER JOIN USER_GROUP_MEMBER ugm ON ugp.UserGroupId = ugm.UserGroupId
                WHERE ugm.UserId = :UserId
                
                UNION
                
                -- 3. Quyền từ các Roles gán trực tiếp cho User
                SELECT rp.PermissionId
                FROM ROLE_PERMISSION rp
                INNER JOIN USER_ROLE ur ON rp.RoleId = ur.RoleId
                WHERE ur.UserId = :UserId
                
                UNION
                
                -- 4. Quyền từ các Roles gán qua Nhóm người dùng
                SELECT rp.PermissionId
                FROM ROLE_PERMISSION rp
                INNER JOIN USER_GROUP_ROLE ugr ON rp.RoleId = ugr.RoleId
                INNER JOIN USER_GROUP_MEMBER ugm ON ugr.UserGroupId = ugm.UserGroupId
                WHERE ugm.UserId = :UserId
            )";
        return await _connection.QueryAsync<string>(sql, new { UserId = userId });
    }
}
