// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Infrastructure\Repositories\UserUnitRoleRepository.cs
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

public class UserUnitRoleRepository : IUserUnitRoleRepository
{
    private readonly string _connectionString;

    public UserUnitRoleRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<UserUnitRole>> GetUnitRolesByUserIdAsync(long userId)
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, UserId, UnitId, RoleId FROM USER_UNIT_ROLE WHERE UserId = :UserId";
        return await connection.QueryAsync<UserUnitRole>(sql, new { UserId = userId });
    }

    public async Task<bool> AssignUnitRolesAsync(long userId, IEnumerable<UserUnitRole> unitRoles)
    {
        using var connection = CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Xóa tất cả quyền đơn vị cũ của user
            await connection.ExecuteAsync(
                "DELETE FROM USER_UNIT_ROLE WHERE UserId = :UserId", 
                new { UserId = userId }, 
                transaction);

            // Thêm danh sách quyền đơn vị mới
            var sql = "INSERT INTO USER_UNIT_ROLE (UserId, UnitId, RoleId) VALUES (:UserId, :UnitId, :RoleId)";
            foreach (var item in unitRoles)
            {
                await connection.ExecuteAsync(sql, new 
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
        using var connection = CreateConnection();
        var sql = "SELECT Id, UserId, UnitId, RoleId FROM USER_UNIT_ROLE WHERE UserId = :UserId AND UnitId = :UnitId";
        return await connection.QueryAsync<UserUnitRole>(sql, new { UserId = userId, UnitId = unitId });
    }
}
