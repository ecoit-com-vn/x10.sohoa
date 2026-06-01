// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Infrastructure\Repositories\MenuRepository.cs
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

public class MenuRepository : IMenuRepository
{
    private readonly string _connectionString;

    public MenuRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<Menu>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode FROM APP_MENU ORDER BY SortOrder, Name";
        return await connection.QueryAsync<Menu>(sql);
    }

    public async Task<Menu?> GetByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode FROM APP_MENU WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<Menu>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Menu menu)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
            VALUES (:Name, :Url, :Icon, :ParentId, :SortOrder, :IsActive, :PermissionCode)
            RETURNING Id INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Name", menu.Name);
        parameters.Add("Url", menu.Url);
        parameters.Add("Icon", menu.Icon);
        parameters.Add("ParentId", menu.ParentId);
        parameters.Add("SortOrder", menu.SortOrder);
        parameters.Add("IsActive", menu.IsActive ? 1 : 0);
        parameters.Add("PermissionCode", menu.PermissionCode);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Menu menu)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE APP_MENU 
            SET Name = :Name, 
                Url = :Url, 
                Icon = :Icon, 
                ParentId = :ParentId, 
                SortOrder = :SortOrder, 
                IsActive = :IsActive, 
                PermissionCode = :PermissionCode 
            WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new 
        {
            menu.Name,
            menu.Url,
            menu.Icon,
            menu.ParentId,
            menu.SortOrder,
            IsActive = menu.IsActive ? 1 : 0,
            menu.PermissionCode,
            menu.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM APP_MENU WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<Menu>> GetMenusByUserPermissionsAsync(IEnumerable<string> permissionCodes)
    {
        using var connection = CreateConnection();
        // Nếu user có phân quyền, lấy các menu có PermissionCode nằm trong danh sách hoặc null.
        // Ngược lại chỉ lấy menu có PermissionCode null.
        var pList = new List<string>(permissionCodes ?? Array.Empty<string>());
        
        string sql;
        if (pList.Count > 0)
        {
            sql = @"
                SELECT Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode 
                FROM APP_MENU 
                WHERE IsActive = 1 AND (PermissionCode IS NULL OR PermissionCode IN :Permissions)
                ORDER BY SortOrder, Name";
            return await connection.QueryAsync<Menu>(sql, new { Permissions = pList });
        }
        else
        {
            sql = @"
                SELECT Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode 
                FROM APP_MENU 
                WHERE IsActive = 1 AND PermissionCode IS NULL
                ORDER BY SortOrder, Name";
            return await connection.QueryAsync<Menu>(sql);
        }
    }
}
