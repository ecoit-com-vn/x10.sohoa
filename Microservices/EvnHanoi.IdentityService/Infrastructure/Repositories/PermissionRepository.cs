using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly IDbConnection _connection;

    public PermissionRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(Permission.Id)}, 
                   {nameof(Permission.Code)}, 
                   {nameof(Permission.Name)}, 
                   {nameof(Permission.Description)}, 
                   {nameof(Permission.IsActive)}, 
                   {nameof(Permission.CreatedAt)}, 
                   {nameof(Permission.CreatedBy)}, 
                   {nameof(Permission.UpdatedAt)}, 
                   {nameof(Permission.UpdatedBy)} 
            FROM PERMISSION 
            ORDER BY {nameof(Permission.Code)}";
        var permissions = (await _connection.QueryAsync<Permission>(sql)).ToList();

        var sqlDetails = $@"
            SELECT {nameof(PermissionDetail.Id)}, 
                   {nameof(PermissionDetail.PermissionId)}, 
                   {nameof(PermissionDetail.ControllerName)}, 
                   {nameof(PermissionDetail.ActionName)} 
            FROM PERMISSION_DETAIL";
        var details = await _connection.QueryAsync<PermissionDetail>(sqlDetails);

        foreach (var p in permissions)
        {
            p.Details = details.Where(d => d.PermissionId == p.Id).ToList();
        }

        return permissions;
    }

    public async Task<Permission?> GetPermissionByIdAsync(string id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(Permission.Id)}, 
                   {nameof(Permission.Code)}, 
                   {nameof(Permission.Name)}, 
                   {nameof(Permission.Description)}, 
                   {nameof(Permission.IsActive)}, 
                   {nameof(Permission.CreatedAt)}, 
                   {nameof(Permission.CreatedBy)}, 
                   {nameof(Permission.UpdatedAt)}, 
                   {nameof(Permission.UpdatedBy)} 
            FROM PERMISSION 
            WHERE {nameof(Permission.Id)} = :Id";
        var p = await _connection.QuerySingleOrDefaultAsync<Permission>(sql, new { Id = id });
        if (p != null)
        {
            var sqlDetails = $@"
                SELECT {nameof(PermissionDetail.Id)}, 
                       {nameof(PermissionDetail.PermissionId)}, 
                       {nameof(PermissionDetail.ControllerName)}, 
                       {nameof(PermissionDetail.ActionName)} 
                FROM PERMISSION_DETAIL 
                WHERE {nameof(PermissionDetail.PermissionId)} = :PermissionId";
            var details = await _connection.QueryAsync<PermissionDetail>(sqlDetails, new { PermissionId = id });
            p.Details = details.ToList();
        }

        return p;
    }

    public async Task<string> CreatePermissionAsync(Permission permission, IEnumerable<PermissionDetail> details)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var permId = string.IsNullOrEmpty(permission.Id) ? Guid.CreateVersion7().ToString() : permission.Id;
            permission.Id = permId;

            var sqlPerm = $@"
                INSERT INTO PERMISSION (
                    {nameof(Permission.Id)}, 
                    {nameof(Permission.Code)}, 
                    {nameof(Permission.Name)}, 
                    {nameof(Permission.Description)}, 
                    {nameof(Permission.IsActive)}, 
                    {nameof(Permission.CreatedBy)}
                )
                VALUES (:Id, :Code, :Name, :Description, :IsActive, :CreatedBy)";

            await _connection.ExecuteAsync(sqlPerm, new
            {
                permission.Id,
                permission.Code,
                permission.Name,
                permission.Description,
                IsActive = permission.IsActive ? 1 : 0,
                permission.CreatedBy
            }, transaction);

            var sqlDetail = $@"
                INSERT INTO PERMISSION_DETAIL (
                    {nameof(PermissionDetail.Id)}, 
                    {nameof(PermissionDetail.PermissionId)}, 
                    {nameof(PermissionDetail.ControllerName)}, 
                    {nameof(PermissionDetail.ActionName)}
                )
                VALUES (:Id, :PermissionId, :ControllerName, :ActionName)";

            foreach (var d in details)
            {
                var detailId = Guid.CreateVersion7().ToString();
                await _connection.ExecuteAsync(sqlDetail, new
                {
                    Id = detailId,
                    PermissionId = permId,
                    d.ControllerName,
                    d.ActionName
                }, transaction);
            }

            transaction.Commit();
            return permId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdatePermissionAsync(Permission permission, IEnumerable<PermissionDetail> details)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var sqlPerm = $@"
                UPDATE PERMISSION 
                SET {nameof(Permission.Code)} = :Code, 
                    {nameof(Permission.Name)} = :Name, 
                    {nameof(Permission.Description)} = :Description, 
                    {nameof(Permission.IsActive)} = :IsActive, 
                    {nameof(Permission.UpdatedAt)} = CURRENT_TIMESTAMP, 
                    {nameof(Permission.UpdatedBy)} = :UpdatedBy 
                WHERE {nameof(Permission.Id)} = :Id";

            var affected = await _connection.ExecuteAsync(sqlPerm, new
            {
                permission.Code,
                permission.Name,
                permission.Description,
                IsActive = permission.IsActive ? 1 : 0,
                permission.UpdatedBy,
                permission.Id
            }, transaction);

            if (affected == 0)
            {
                transaction.Rollback();
                return false;
            }

            // Clear old details
            await _connection.ExecuteAsync($@"
                DELETE FROM PERMISSION_DETAIL 
                WHERE {nameof(PermissionDetail.PermissionId)} = :PermissionId", 
                new { PermissionId = permission.Id }, 
                transaction);

            // Insert new details
            var sqlDetail = $@"
                INSERT INTO PERMISSION_DETAIL (
                    {nameof(PermissionDetail.Id)}, 
                    {nameof(PermissionDetail.PermissionId)}, 
                    {nameof(PermissionDetail.ControllerName)}, 
                    {nameof(PermissionDetail.ActionName)}
                )
                VALUES (:Id, :PermissionId, :ControllerName, :ActionName)";

            foreach (var d in details)
            {
                var detailId = Guid.CreateVersion7().ToString();
                await _connection.ExecuteAsync(sqlDetail, new
                {
                    Id = detailId,
                    PermissionId = permission.Id,
                    d.ControllerName,
                    d.ActionName
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

    public async Task<bool> DeletePermissionAsync(string id)
    {
        var sql = $"DELETE FROM PERMISSION WHERE {nameof(Permission.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<bool> AssignPermissionsToUserAsync(string userId, IEnumerable<string> permissionIds)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteAsync($@"
                DELETE FROM USER_PERMISSION 
                WHERE {nameof(UserPermission.UserId)} = :UserId", 
                new { UserId = userId }, 
                transaction);

            var sql = $@"
                INSERT INTO USER_PERMISSION (
                    {nameof(UserPermission.UserId)}, 
                    {nameof(UserPermission.PermissionId)}
                ) 
                VALUES (:UserId, :PermissionId)";

            foreach (var permId in permissionIds)
            {
                await _connection.ExecuteAsync(sql, new { UserId = userId, PermissionId = permId }, transaction);
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

    public async Task<bool> AssignPermissionsToUserGroupAsync(long userGroupId, IEnumerable<string> permissionIds)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteAsync($@"
                DELETE FROM USER_GROUP_PERMISSION 
                WHERE {nameof(UserGroupPermission.UserGroupId)} = :UserGroupId", 
                new { UserGroupId = userGroupId }, 
                transaction);

            var sql = $@"
                INSERT INTO USER_GROUP_PERMISSION (
                    {nameof(UserGroupPermission.UserGroupId)}, 
                    {nameof(UserGroupPermission.PermissionId)}
                ) 
                VALUES (:UserGroupId, :PermissionId)";

            foreach (var permId in permissionIds)
            {
                await _connection.ExecuteAsync(sql, new { UserGroupId = userGroupId, PermissionId = permId }, transaction);
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

    public async Task<IEnumerable<PermissionDetail>> GetAllowedActionsForUserAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT pd.{nameof(PermissionDetail.Id)}, 
                   pd.{nameof(PermissionDetail.PermissionId)}, 
                   pd.{nameof(PermissionDetail.ControllerName)}, 
                   pd.{nameof(PermissionDetail.ActionName)}
            FROM PERMISSION_DETAIL pd
            INNER JOIN PERMISSION p ON pd.{nameof(PermissionDetail.PermissionId)} = p.{nameof(Permission.Id)}
            WHERE p.{nameof(Permission.IsActive)} = 1 AND pd.{nameof(PermissionDetail.PermissionId)} IN (
                -- 1. Quyền trực tiếp
                SELECT {nameof(UserPermission.PermissionId)} 
                FROM USER_PERMISSION 
                WHERE {nameof(UserPermission.UserId)} = :UserId

                UNION
                
                -- 2. Quyền qua Nhóm người dùng
                SELECT ugp.{nameof(UserGroupPermission.PermissionId)} 
                FROM USER_GROUP_PERMISSION ugp
                INNER JOIN USER_GROUP_MEMBER ugm ON ugp.{nameof(UserGroupPermission.UserGroupId)} = ugm.UserGroupId
                WHERE ugm.UserId = :UserId
                
                UNION
                
                -- 3. Quyền từ Roles gán trực tiếp cho User
                -- Role GLOBAL: lấy tất cả nhóm gắn (SYSTEM + UNIT)
                -- Role UNIT: SYSTEM không áp dụng qua nhánh này; UNIT group phải khớp Role.OrganizationUnitId qua PERMISSION_GROUP_UNIT
                SELECT pgp.PermissionId
                FROM USER_ROLE ur
                INNER JOIN ROLE r ON ur.RoleId = r.Id
                INNER JOIN ROLE_PERMISSION_GROUP rpg ON r.Id = rpg.RoleId
                INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id
                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
                INNER JOIN PERMISSION_GROUP_PERMISSION pgp ON pgp.PermissionGroupId = pg.Id
                WHERE ur.UserId = :UserId
                  AND (
                    r.ScopeTypeId = 1
                    OR st.Code = 'GLOBAL'
                    OR EXISTS (
                        SELECT 1 FROM PERMISSION_GROUP_UNIT pgu
                        WHERE pgu.PermissionGroupId = pg.Id
                          AND pgu.OrganizationUnitId = r.OrganizationUnitId
                    )
                  )
                
                UNION
                
                -- 4. Quyền từ Roles gán qua Nhóm người dùng
                SELECT pgp.PermissionId
                FROM USER_GROUP_MEMBER ugm
                INNER JOIN USER_GROUP_ROLE ugr ON ugm.UserGroupId = ugr.UserGroupId
                INNER JOIN ROLE r ON ugr.RoleId = r.Id
                INNER JOIN ROLE_PERMISSION_GROUP rpg ON r.Id = rpg.RoleId
                INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id
                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
                INNER JOIN PERMISSION_GROUP_PERMISSION pgp ON pgp.PermissionGroupId = pg.Id
                WHERE ugm.UserId = :UserId
                  AND (
                    r.ScopeTypeId = 1
                    OR st.Code = 'GLOBAL'
                    OR EXISTS (
                        SELECT 1 FROM PERMISSION_GROUP_UNIT pgu
                        WHERE pgu.PermissionGroupId = pg.Id
                          AND pgu.OrganizationUnitId = r.OrganizationUnitId
                    )
                  )

                UNION

                -- 5. Quyền từ Roles gán theo đơn vị (USER_UNIT_ROLE)
                -- Chỉ lấy nhóm SYSTEM hoặc nhóm UNIT có mapping khớp UnitId ngữ cảnh
                SELECT pgp.PermissionId
                FROM USER_UNIT_ROLE uur
                INNER JOIN ROLE_PERMISSION_GROUP rpg ON uur.RoleId = rpg.RoleId
                INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id
                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
                INNER JOIN PERMISSION_GROUP_PERMISSION pgp ON pgp.PermissionGroupId = pg.Id
                WHERE uur.UserId = :UserId
                  AND (
                    st.Code = 'GLOBAL'
                    OR EXISTS (
                        SELECT 1 FROM PERMISSION_GROUP_UNIT pgu
                        WHERE pgu.PermissionGroupId = pg.Id
                          AND pgu.OrganizationUnitId = uur.UnitId
                    )
                  )
            )";
        return await _connection.QueryAsync<PermissionDetail>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<string>> GetPermissionsByUserIdAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserPermission.PermissionId)}
            FROM USER_PERMISSION
            WHERE {nameof(UserPermission.UserId)} = :UserId";
        return await _connection.QueryAsync<string>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<string>> GetPermissionsByUserGroupIdAsync(long userGroupId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserGroupPermission.PermissionId)} 
            FROM USER_GROUP_PERMISSION 
            WHERE {nameof(UserGroupPermission.UserGroupId)} = :UserGroupId";
        return await _connection.QueryAsync<string>(sql, new { UserGroupId = userGroupId });
    }

    public async Task<IEnumerable<string>> GetPermissionCodesByUserIdAsync(string userId)
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
                SELECT pgp.PermissionId
                FROM USER_ROLE ur
                INNER JOIN ROLE r ON ur.RoleId = r.Id
                INNER JOIN ROLE_PERMISSION_GROUP rpg ON r.Id = rpg.RoleId
                INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id
                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
                INNER JOIN PERMISSION_GROUP_PERMISSION pgp ON pgp.PermissionGroupId = pg.Id
                WHERE ur.UserId = :UserId
                  AND (
                    r.ScopeTypeId = 1
                    OR st.Code = 'GLOBAL'
                    OR EXISTS (
                        SELECT 1 FROM PERMISSION_GROUP_UNIT pgu
                        WHERE pgu.PermissionGroupId = pg.Id
                          AND pgu.OrganizationUnitId = r.OrganizationUnitId
                    )
                  )
                
                UNION
                
                -- 4. Quyền từ các Roles gán qua Nhóm người dùng
                SELECT pgp.PermissionId
                FROM USER_GROUP_MEMBER ugm
                INNER JOIN USER_GROUP_ROLE ugr ON ugm.UserGroupId = ugr.UserGroupId
                INNER JOIN ROLE r ON ugr.RoleId = r.Id
                INNER JOIN ROLE_PERMISSION_GROUP rpg ON r.Id = rpg.RoleId
                INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id
                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
                INNER JOIN PERMISSION_GROUP_PERMISSION pgp ON pgp.PermissionGroupId = pg.Id
                WHERE ugm.UserId = :UserId
                  AND (
                    r.ScopeTypeId = 1
                    OR st.Code = 'GLOBAL'
                    OR EXISTS (
                        SELECT 1 FROM PERMISSION_GROUP_UNIT pgu
                        WHERE pgu.PermissionGroupId = pg.Id
                          AND pgu.OrganizationUnitId = r.OrganizationUnitId
                    )
                  )

                UNION

                -- 5. Quyền từ Roles gán theo đơn vị (USER_UNIT_ROLE)
                SELECT pgp.PermissionId
                FROM USER_UNIT_ROLE uur
                INNER JOIN ROLE_PERMISSION_GROUP rpg ON uur.RoleId = rpg.RoleId
                INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id
                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id
                INNER JOIN PERMISSION_GROUP_PERMISSION pgp ON pgp.PermissionGroupId = pg.Id
                WHERE uur.UserId = :UserId
                  AND (
                    st.Code = 'GLOBAL'
                    OR EXISTS (
                        SELECT 1 FROM PERMISSION_GROUP_UNIT pgu
                        WHERE pgu.PermissionGroupId = pg.Id
                          AND pgu.OrganizationUnitId = uur.UnitId
                    )
                  )
            )";
        return await _connection.QueryAsync<string>(sql, new { UserId = userId });
    }
}
