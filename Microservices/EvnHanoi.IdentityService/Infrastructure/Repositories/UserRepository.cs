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
                   u.PHONE_NUMBER AS {nameof(User.PhoneNumber)},
                   u.{nameof(User.PasswordHash)}, 
                   u.{nameof(User.IsActive)}, 
                   u.{nameof(User.OrganizationUnitId)},
                   u.{nameof(User.PositionId)},
                   u.{nameof(User.PositionName)},
                   u.{nameof(User.AvatarObjectKey)},
                   u.AUTH_PROVIDER AS {nameof(User.AuthProvider)},
                   u.SSO_USER_ID AS {nameof(User.SsoUserId)},
                   u.SSO_USERNAME AS {nameof(User.SsoUsername)},
                   u.SSO_NS_ID AS {nameof(User.SsoNsId)},
                   u.SSO_DEPT_ID AS {nameof(User.SsoDeptId)},
                   u.SSO_ORG_ID AS {nameof(User.SsoOrgId)},
                   u.STAFF_CODE AS {nameof(User.StaffCode)},
                   u.IS_SSO_ENABLED AS {nameof(User.IsSsoEnabled)},
                   u.{nameof(User.AccessFailedCount)}, 
                   u.{nameof(User.LockoutEnd)}, 
                   u.{nameof(User.LockoutEnabled)},
                   o.{nameof(OrganizationUnit.Id)}, 
                   o.{nameof(OrganizationUnit.Code)}, 
                   o.{nameof(OrganizationUnit.Name)}, 
                   o.{nameof(OrganizationUnit.ParentId)}, 
                   o.{nameof(OrganizationUnit.Description)}
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(User.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)} AND o.IsDeleted = 0
            WHERE u.UserName = :Username AND u.IsDeleted = 0";
            
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
                   u.PHONE_NUMBER AS {nameof(User.PhoneNumber)},
                   u.{nameof(User.PasswordHash)}, 
                   u.{nameof(User.IsActive)}, 
                   u.{nameof(User.OrganizationUnitId)},
                   u.{nameof(User.PositionId)},
                   u.{nameof(User.PositionName)},
                   u.{nameof(User.AvatarObjectKey)},
                   u.AUTH_PROVIDER AS {nameof(User.AuthProvider)},
                   u.SSO_USER_ID AS {nameof(User.SsoUserId)},
                   u.SSO_USERNAME AS {nameof(User.SsoUsername)},
                   u.SSO_NS_ID AS {nameof(User.SsoNsId)},
                   u.SSO_DEPT_ID AS {nameof(User.SsoDeptId)},
                   u.SSO_ORG_ID AS {nameof(User.SsoOrgId)},
                   u.STAFF_CODE AS {nameof(User.StaffCode)},
                   u.IS_SSO_ENABLED AS {nameof(User.IsSsoEnabled)},
                   u.{nameof(User.AccessFailedCount)}, 
                   u.{nameof(User.LockoutEnd)}, 
                   u.{nameof(User.LockoutEnabled)},
                   o.{nameof(OrganizationUnit.Id)}, 
                   o.{nameof(OrganizationUnit.Code)}, 
                   o.{nameof(OrganizationUnit.Name)}, 
                   o.{nameof(OrganizationUnit.ParentId)}, 
                   o.{nameof(OrganizationUnit.Description)}
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(User.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)} AND o.IsDeleted = 0
            WHERE u.IsDeleted = 0";
            
        return await _connection.QueryAsync<User, OrganizationUnit, User>(
            sql, 
            (user, unit) => {
                user.OrganizationUnit = unit;
                return user;
            }, 
            splitOn: "Id"
        );
    }

    public async Task<(IEnumerable<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, long? organizationUnitId = null, bool? isActive = null, bool includeDescendants = false)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        
        var conditions = new List<string> { "u.IsDeleted = 0" };
        var parameters = new DynamicParameters();
        
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            conditions.Add("(UPPER(u.UserName) LIKE UPPER(:Keyword) OR UPPER(u.FullName) LIKE UPPER(:Keyword) OR UPPER(u.Email) LIKE UPPER(:Keyword))");
            parameters.Add("Keyword", $"%{keyword}%");
        }
        
        if (organizationUnitId.HasValue && organizationUnitId.Value > 0)
        {
            if (includeDescendants)
            {
                conditions.Add(@"u.OrganizationUnitId IN (
                    SELECT Id FROM ORGANIZATION_UNIT
                    WHERE IsDeleted = 0
                    START WITH Id = :OrganizationUnitId
                    CONNECT BY PRIOR Id = ParentId)");
            }
            else
            {
                conditions.Add("u.OrganizationUnitId = :OrganizationUnitId");
            }
            parameters.Add("OrganizationUnitId", organizationUnitId.Value);
        }
        
        if (isActive.HasValue)
        {
            conditions.Add("u.IsActive = :IsActive");
            parameters.Add("IsActive", isActive.Value ? 1 : 0);
        }
        
        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        
        var countSql = $"SELECT COUNT(*) FROM APP_USER u LEFT JOIN ORGANIZATION_UNIT o ON u.OrganizationUnitId = o.Id AND o.IsDeleted = 0 {whereClause}";
        var offset = (page - 1) * pageSize;
        
        var sql = $@"
            SELECT Id, Username, Email, FullName, PasswordHash, IsActive, IsSsoEnabled, OrganizationUnitId, PositionId, PositionName, AvatarObjectKey, AccessFailedCount, LockoutEnd, LockoutEnabled,
                   OrgId AS Id, Code, Name, ParentId, Description
            FROM (
                SELECT u.Id AS Id, 
                       u.UserName AS Username, 
                       u.Email AS Email, 
                       u.FullName AS FullName, 
                       u.PasswordHash AS PasswordHash, 
                       u.IsActive AS IsActive, 
                       u.IS_SSO_ENABLED AS IsSsoEnabled,
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
                       ROW_NUMBER() OVER (
                           ORDER BY
                               u.IsActive DESC NULLS LAST,
                               u.FullName ASC,
                               u.UserName ASC
                       ) AS RN
                FROM APP_USER u
                LEFT JOIN ORGANIZATION_UNIT o ON u.OrganizationUnitId = o.Id AND o.IsDeleted = 0
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
                   u.PHONE_NUMBER AS {nameof(User.PhoneNumber)},
                   u.{nameof(User.PasswordHash)}, 
                   u.{nameof(User.IsActive)}, 
                   u.{nameof(User.OrganizationUnitId)},
                   u.{nameof(User.PositionId)},
                   u.{nameof(User.PositionName)},
                   u.{nameof(User.AvatarObjectKey)},
                   u.AUTH_PROVIDER AS {nameof(User.AuthProvider)},
                   u.SSO_USER_ID AS {nameof(User.SsoUserId)},
                   u.SSO_USERNAME AS {nameof(User.SsoUsername)},
                   u.SSO_NS_ID AS {nameof(User.SsoNsId)},
                   u.SSO_DEPT_ID AS {nameof(User.SsoDeptId)},
                   u.SSO_ORG_ID AS {nameof(User.SsoOrgId)},
                   u.STAFF_CODE AS {nameof(User.StaffCode)},
                   u.IS_SSO_ENABLED AS {nameof(User.IsSsoEnabled)},
                   u.{nameof(User.AccessFailedCount)}, 
                   u.{nameof(User.LockoutEnd)}, 
                   u.{nameof(User.LockoutEnabled)},
                   o.{nameof(OrganizationUnit.Id)}, 
                   o.{nameof(OrganizationUnit.Code)}, 
                   o.{nameof(OrganizationUnit.Name)}, 
                   o.{nameof(OrganizationUnit.ParentId)}, 
                   o.{nameof(OrganizationUnit.Description)}
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(User.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)} AND o.IsDeleted = 0
            WHERE u.{nameof(User.Id)} = :Id AND u.IsDeleted = 0";
            
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
                IS_SSO_ENABLED = :IsSsoEnabled,
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
            IsSsoEnabled = user.IsSsoEnabled ? 1 : 0,
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
              AND Id <> :UserId
              AND IsDeleted = 0";

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
                PHONE_NUMBER,
                {nameof(User.PasswordHash)}, 
                {nameof(User.IsActive)}, 
                {nameof(User.OrganizationUnitId)},
                {nameof(User.PositionId)},
                {nameof(User.PositionName)},
                AUTH_PROVIDER,
                SSO_USER_ID,
                SSO_USERNAME,
                SSO_NS_ID,
                SSO_DEPT_ID,
                SSO_ORG_ID,
                STAFF_CODE,
                IS_SSO_ENABLED,
                {nameof(User.AccessFailedCount)},
                {nameof(User.LockoutEnabled)}
            )
            VALUES (:Id, :Username, :Email, :FullName, :PhoneNumber, :PasswordHash, :IsActive, :OrganizationUnitId, :PositionId, :PositionName, :AuthProvider, :SsoUserId, :SsoUsername, :SsoNsId, :SsoDeptId, :SsoOrgId, :StaffCode, :IsSsoEnabled, :AccessFailedCount, :LockoutEnabled)";
            
        await _connection.ExecuteAsync(sql, new
        {
            user.Id,
            Username = user.Username,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            user.PasswordHash,
            IsActive = user.IsActive ? 1 : 0,
            user.OrganizationUnitId,
            user.PositionId,
            user.PositionName,
            user.AuthProvider,
            user.SsoUserId,
            user.SsoUsername,
            user.SsoNsId,
            user.SsoDeptId,
            user.SsoOrgId,
            user.StaffCode,
            IsSsoEnabled = user.IsSsoEnabled ? 1 : 0,
            user.AccessFailedCount,
            LockoutEnabled = user.LockoutEnabled ? 1 : 0
        });
        return user.Id;
    }

    public async Task DeleteAsync(string id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE APP_USER 
            SET IsDeleted = 1,
                IsActive = 0,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE {nameof(User.Id)} = :Id AND IsDeleted = 0";
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

    public async Task<IEnumerable<UserLookupDto>> GetUsersLookupAsync(string? roleCodeFilter)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"
            SELECT u.Id, u.UserName AS Username, u.FullName, r.Code AS RoleCode
            FROM APP_USER u
            LEFT JOIN (
                SELECT ur.UserId, r.Code
                FROM ROLE r
                INNER JOIN (
                    SELECT UserId, RoleId FROM USER_ROLE
                    UNION
                    SELECT ugm.UserId, ugr.RoleId 
                    FROM USER_GROUP_MEMBER ugm
                    INNER JOIN USER_GROUP_ROLE ugr ON ugm.UserGroupId = ugr.UserGroupId
                ) ur ON r.Id = ur.RoleId
            ) r ON u.Id = r.UserId
            WHERE u.IsDeleted = 0 AND u.IsActive = 1
            ORDER BY u.UserName ASC";

        var userDict = new Dictionary<string, UserLookupDto>();
        await _connection.QueryAsync<UserLookupDto, string, UserLookupDto>(
            sql,
            (user, roleCode) =>
            {
                if (!userDict.TryGetValue(user.Id, out var existingUser))
                {
                    existingUser = user;
                    existingUser.Roles = new List<string>();
                    userDict.Add(user.Id, existingUser);
                }
                if (!string.IsNullOrEmpty(roleCode))
                {
                    ((List<string>)existingUser.Roles).Add(roleCode);
                }
                return user;
            },
            splitOn: "RoleCode"
        );

        var result = userDict.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(roleCodeFilter))
        {
            var filter = roleCodeFilter.Trim();
            result = result.Where(u => u.Roles.Any(r => string.Equals(r, filter, StringComparison.OrdinalIgnoreCase)));
        }
        return result;
    }

    public async Task<IEnumerable<UserLookupDto>> GetEligibleAssigneesAsync(
        List<long> systemGroupIds,
        List<long> unitGroupIds,
        List<string> assigneeIds,
        long? unitId,
        string? keyword,
        int page,
        int pageSize)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        // Hợp nhất hai danh sách nhóm quyền
        var allGroupIds = systemGroupIds.Union(unitGroupIds).Distinct().ToList();
        var hasAssigneeIds = assigneeIds.Count > 0;

        // Xây dựng câu truy vấn lấy người dùng thuộc ít nhất 1 trong các nguồn được yêu cầu
        // Kênh 1: Qua USER_ROLE → ROLE_PERMISSION_GROUP
        // Kênh 2: Qua USER_UNIT_ROLE → ROLE_PERMISSION_GROUP
        // Kênh 3: Qua USER_GROUP_MEMBER → USER_GROUP_ROLE → ROLE_PERMISSION_GROUP
        // Kênh 4: "Người cụ thể" — ID người dùng được cấu hình trực tiếp trên bước
        var sql = @"
            SELECT DISTINCT u.Id, u.UserName AS Username, u.FullName,
                            u.OrganizationUnitId, o.Name AS OrganizationUnitName
            FROM APP_USER u
            LEFT JOIN ORGANIZATION_UNIT o ON u.OrganizationUnitId = o.Id AND o.IsDeleted = 0
            WHERE u.IsDeleted = 0
              AND u.IsActive = 1
              AND (
                  -- Kênh 1: gán trực tiếp qua USER_ROLE
                  EXISTS (
                      SELECT 1 FROM USER_ROLE ur
                      INNER JOIN ROLE_PERMISSION_GROUP rpg ON ur.RoleId = rpg.RoleId
                      WHERE ur.UserId = u.Id AND rpg.PermissionGroupId IN :GroupIds
                  )
                  -- Kênh 2: gán qua USER_UNIT_ROLE (vai trò theo đơn vị)
                  OR EXISTS (
                      SELECT 1 FROM USER_UNIT_ROLE uur
                      INNER JOIN ROLE_PERMISSION_GROUP rpg ON uur.RoleId = rpg.RoleId
                      WHERE uur.UserId = u.Id AND rpg.PermissionGroupId IN :GroupIds
                  )
                  -- Kênh 3: gán qua nhóm người dùng
                  OR EXISTS (
                      SELECT 1 FROM USER_GROUP_MEMBER ugm
                      INNER JOIN USER_GROUP_ROLE ugr ON ugm.UserGroupId = ugr.UserGroupId
                      INNER JOIN ROLE_PERMISSION_GROUP rpg ON ugr.RoleId = rpg.RoleId
                      WHERE ugm.UserId = u.Id AND rpg.PermissionGroupId IN :GroupIds
                  )"
                  + (hasAssigneeIds ? " OR u.Id IN :AssigneeIds" : "") + @"
              )";

        var parameters = new DynamicParameters();
        parameters.Add("GroupIds", allGroupIds.Count > 0 ? allGroupIds : new List<long> { -1 });
        if (hasAssigneeIds) parameters.Add("AssigneeIds", assigneeIds);

        // Lọc theo đơn vị nếu RequireSameUnit = true
        if (unitId.HasValue)
        {
            sql += " AND u.OrganizationUnitId = :UnitId";
            parameters.Add("UnitId", unitId.Value);
        }

        // Lọc theo từ khóa
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += @" AND (LOWER(u.FullName) LIKE :Keyword OR LOWER(u.UserName) LIKE :Keyword)";
            parameters.Add("Keyword", $"%{keyword.ToLowerInvariant()}%");
        }

        sql += " ORDER BY u.FullName ASC";

        // Phân trang thủ công (Oracle 12c+ hỗ trợ OFFSET/FETCH)
        sql += " OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var result = await _connection.QueryAsync<UserLookupDto>(sql, parameters);
        return result;
    }
}
