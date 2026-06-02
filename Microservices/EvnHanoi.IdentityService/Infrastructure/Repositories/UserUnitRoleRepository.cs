using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UserUnitRoleRepository : IUserUnitRoleRepository
{
    private readonly IDbConnection _connection;

    public UserUnitRoleRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<UserUnitRole>> GetUnitRolesByUserIdAsync(long userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserUnitRole.Id)}, 
                   {nameof(UserUnitRole.UserId)}, 
                   {nameof(UserUnitRole.UnitId)}, 
                   {nameof(UserUnitRole.RoleId)} 
            FROM USER_UNIT_ROLE 
            WHERE {nameof(UserUnitRole.UserId)} = :UserId";
        return await _connection.QueryAsync<UserUnitRole>(sql, new { UserId = userId });
    }

    public async Task<bool> AssignUnitRolesAsync(long userId, IEnumerable<UserUnitRole> unitRoles)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            // Xóa tất cả quyền đơn vị cũ của user
            await _connection.ExecuteAsync(
                $"DELETE FROM USER_UNIT_ROLE WHERE {nameof(UserUnitRole.UserId)} = :UserId", 
                new { UserId = userId }, 
                transaction);

            // Thêm danh sách quyền đơn vị mới
            var sql = $@"
                INSERT INTO USER_UNIT_ROLE (
                    {nameof(UserUnitRole.UserId)}, 
                    {nameof(UserUnitRole.UnitId)}, 
                    {nameof(UserUnitRole.RoleId)}
                ) 
                VALUES (:UserId, :UnitId, :RoleId)";
            foreach (var item in unitRoles)
            {
                await _connection.ExecuteAsync(sql, new 
                {
                    UserId = userId,
                    UnitId = item.UnitId,
                    RoleId = item.RoleId
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

    public async Task<IEnumerable<UserUnitRole>> GetUnitRolesByUserAndUnitAsync(long userId, long unitId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserUnitRole.Id)}, 
                   {nameof(UserUnitRole.UserId)}, 
                   {nameof(UserUnitRole.UnitId)}, 
                   {nameof(UserUnitRole.RoleId)} 
            FROM USER_UNIT_ROLE 
            WHERE {nameof(UserUnitRole.UserId)} = :UserId AND {nameof(UserUnitRole.UnitId)} = :UnitId";
        return await _connection.QueryAsync<UserUnitRole>(sql, new { UserId = userId, UnitId = unitId });
    }
}
