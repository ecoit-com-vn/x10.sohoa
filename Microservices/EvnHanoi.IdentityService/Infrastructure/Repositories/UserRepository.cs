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
    private readonly IPermissionRepository _permissionRepository;

    public UserRepository(IDbConnection connection, IPermissionRepository permissionRepository)
    {
        _connection = connection;
        _permissionRepository = permissionRepository;
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
                   u.{nameof(User.PositionId)},
                   u.{nameof(User.PositionName)},
                   u.{nameof(User.AvatarObjectKey)},
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
                   u.{nameof(User.PositionId)},
                   u.{nameof(User.PositionName)},
                   u.{nameof(User.AvatarObjectKey)},
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

    public async Task<(IEnumerable<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, long? organizationUnitId = null, bool? isActive = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        
        var conditions = new List<string>();
        var parameters = new DynamicParameters();
        
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            conditions.Add("(UPPER(u.UserName) LIKE UPPER(:Keyword) OR UPPER(u.FullName) LIKE UPPER(:Keyword) OR UPPER(u.Email) LIKE UPPER(:Keyword))");
            parameters.Add("Keyword", $"%{keyword}%");
        }
        
        if (organizationUnitId.HasValue && organizationUnitId.Value > 0)
        {
            conditions.Add("u.OrganizationUnitId = :OrganizationUnitId");
            parameters.Add("OrganizationUnitId", organizationUnitId.Value);
        }
        
        if (isActive.HasValue)
        {
            conditions.Add("u.IsActive = :IsActive");
            parameters.Add("IsActive", isActive.Value ? 1 : 0);
        }
        
        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        
        var countSql = $"SELECT COUNT(*) FROM APP_USER u LEFT JOIN ORGANIZATION_UNIT o ON u.OrganizationUnitId = o.Id {whereClause}";
        var offset = (page - 1) * pageSize;
        
        var sql = $@"
            SELECT Id, Username, Email, FullName, PasswordHash, IsActive, OrganizationUnitId, PositionId, PositionName, AvatarObjectKey, AccessFailedCount, LockoutEnd, LockoutEnabled,
                   OrgId AS Id, Code, Name, ParentId, Description
            FROM (
                SELECT u.Id AS Id, 
                       u.UserName AS Username, 
                       u.Email AS Email, 
                       u.FullName AS FullName, 
                       u.PasswordHash AS PasswordHash, 
                       u.IsActive AS IsActive, 
                       u.OrganizationUnitId AS OrganizationUnitId,
                       u.PositionId AS PositionId,
                       u.PositionName AS PositionName,
                       u.AvatarObjectKey AS AvatarObjectKey,
                       u.AccessFailedCount AS AccessFailedCount, 
                       u.LockoutEnd AS LockoutEnd, 
                       u.LockoutEnabled AS LockoutEnabled,
                       o.Id AS OrgId, 
                       o.Code AS Code, 
                       o.Name AS Name, 
                       o.ParentId AS ParentId, 
                       o.Description AS Description,
                       ROW_NUMBER() OVER (ORDER BY u.UserName ASC) AS RN
                FROM APP_USER u
                LEFT JOIN ORGANIZATION_UNIT o ON u.OrganizationUnitId = o.Id
                {whereClause}
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";
            
        parameters.Add("Offset", offset);
        parameters.Add("OffsetPlusSize", offset + pageSize);
        
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await _connection.QueryAsync<User, OrganizationUnit, User>(
            sql, 
            (user, unit) => {
                user.OrganizationUnit = unit;
                return user;
            },
            parameters,
            splitOn: "Id"
        );
        
        return (items, totalCount);
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
                   u.{nameof(User.PositionId)},
                   u.{nameof(User.PositionName)},
                   u.{nameof(User.AvatarObjectKey)},
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
                {nameof(User.PositionId)} = :PositionId,
                {nameof(User.PositionName)} = :PositionName,
                {nameof(User.AvatarObjectKey)} = :AvatarObjectKey,
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
            user.PositionId,
            user.PositionName,
            user.AvatarObjectKey,
            LockoutEnabled = user.LockoutEnabled ? 1 : 0,
            user.Id
        });
    }

    public async Task UpdateProfileAsync(User user)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE APP_USER
            SET {nameof(User.Email)} = :Email,
                {nameof(User.FullName)} = :FullName,
                {nameof(User.PositionId)} = :PositionId,
                {nameof(User.PositionName)} = :PositionName,
                UpdatedAt = CURRENT_TIMESTAMP,
                UpdatedBy = :UpdatedBy
            WHERE {nameof(User.Id)} = :Id";

        await _connection.ExecuteAsync(sql, new
        {
            user.Email,
            user.FullName,
            user.PositionId,
            user.PositionName,
            UpdatedBy = user.Id,
            user.Id
        });
    }

    public async Task UpdateAvatarAsync(string userId, string? avatarObjectKey)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE APP_USER
            SET {nameof(User.AvatarObjectKey)} = :AvatarObjectKey,
                UpdatedAt = CURRENT_TIMESTAMP,
                UpdatedBy = :UpdatedBy
            WHERE {nameof(User.Id)} = :Id";

        await _connection.ExecuteAsync(sql, new
        {
            AvatarObjectKey = avatarObjectKey,
            UpdatedBy = userId,
            Id = userId
        });
    }

    public async Task UpdatePasswordAsync(string userId, string passwordHash)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE APP_USER
            SET {nameof(User.PasswordHash)} = :PasswordHash,
                UpdatedAt = CURRENT_TIMESTAMP,
                UpdatedBy = :UpdatedBy
            WHERE {nameof(User.Id)} = :Id";

        await _connection.ExecuteAsync(sql, new
        {
            PasswordHash = passwordHash,
            UpdatedBy = userId,
            Id = userId
        });
    }

    public async Task<bool> EmailExistsForOtherUserAsync(string email, string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"
            SELECT COUNT(1)
            FROM APP_USER
            WHERE UPPER(Email) = UPPER(:Email)
              AND Id <> :UserId";

        var count = await _connection.ExecuteScalarAsync<int>(sql, new
        {
            Email = email,
            UserId = userId
        });

        return count > 0;
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
                {nameof(User.PositionId)},
                {nameof(User.PositionName)},
                {nameof(User.AccessFailedCount)},
                {nameof(User.LockoutEnabled)}
            )
            VALUES (:Id, :Username, :Email, :FullName, :PasswordHash, :IsActive, :OrganizationUnitId, :PositionId, :PositionName, :AccessFailedCount, :LockoutEnabled)";
            
        await _connection.ExecuteAsync(sql, new
        {
            user.Id,
            Username = user.Username,
            user.Email,
            user.FullName,
            user.PasswordHash,
            IsActive = user.IsActive ? 1 : 0,
            user.OrganizationUnitId,
            user.PositionId,
            user.PositionName,
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
        return await _permissionRepository.GetPermissionCodesByUserIdAsync(userId);
    }

    public async Task<IEnumerable<long>> GetDirectRoleIdsByUserIdAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = "SELECT RoleId FROM USER_ROLE WHERE UserId = :UserId";
        return await _connection.QueryAsync<long>(sql, new { UserId = userId });
    }

    public async Task<bool> AssignRolesToUserAsync(string userId, IEnumerable<long> roleIds)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            // Clear existing roles mapping
            await _connection.ExecuteAsync(
                "DELETE FROM USER_ROLE WHERE UserId = :UserId", 
                new { UserId = userId }, 
                transaction);

            // Insert new roles mapping
            var sql = "INSERT INTO USER_ROLE (UserId, RoleId) VALUES (:UserId, :RoleId)";
            foreach (var roleId in roleIds)
            {
                await _connection.ExecuteAsync(sql, new { UserId = userId, RoleId = roleId }, transaction);
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
