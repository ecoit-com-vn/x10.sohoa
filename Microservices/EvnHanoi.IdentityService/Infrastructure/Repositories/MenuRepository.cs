using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class MenuRepository : IMenuRepository
{
    private readonly IDbConnection _connection;

    public MenuRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<Menu>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(Menu.Id)}, 
                   {nameof(Menu.Name)}, 
                   {nameof(Menu.Url)}, 
                   {nameof(Menu.Icon)}, 
                   {nameof(Menu.ParentId)}, 
                   {nameof(Menu.SortOrder)}, 
                   {nameof(Menu.IsActive)}, 
                   {nameof(Menu.PermissionCode)} 
            FROM APP_MENU 
            ORDER BY {nameof(Menu.SortOrder)}, {nameof(Menu.Name)}";
        return await _connection.QueryAsync<Menu>(sql);
    }

    public async Task<Menu?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(Menu.Id)}, 
                   {nameof(Menu.Name)}, 
                   {nameof(Menu.Url)}, 
                   {nameof(Menu.Icon)}, 
                   {nameof(Menu.ParentId)}, 
                   {nameof(Menu.SortOrder)}, 
                   {nameof(Menu.IsActive)}, 
                   {nameof(Menu.PermissionCode)} 
            FROM APP_MENU 
            WHERE {nameof(Menu.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<Menu>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Menu menu)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            INSERT INTO APP_MENU (
                {nameof(Menu.Name)}, 
                {nameof(Menu.Url)}, 
                {nameof(Menu.Icon)}, 
                {nameof(Menu.ParentId)}, 
                {nameof(Menu.SortOrder)}, 
                {nameof(Menu.IsActive)}, 
                {nameof(Menu.PermissionCode)}
            )
            VALUES (:Name, :Url, :Icon, :ParentId, :SortOrder, :IsActive, :PermissionCode)
            RETURNING {nameof(Menu.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Name", menu.Name);
        parameters.Add("Url", menu.Url);
        parameters.Add("Icon", menu.Icon);
        parameters.Add("ParentId", menu.ParentId);
        parameters.Add("SortOrder", menu.SortOrder);
        parameters.Add("IsActive", menu.IsActive ? 1 : 0);
        parameters.Add("PermissionCode", menu.PermissionCode);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Menu menu)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE APP_MENU 
            SET {nameof(Menu.Name)} = :Name, 
                {nameof(Menu.Url)} = :Url, 
                {nameof(Menu.Icon)} = :Icon, 
                {nameof(Menu.ParentId)} = :ParentId, 
                {nameof(Menu.SortOrder)} = :SortOrder, 
                {nameof(Menu.IsActive)} = :IsActive, 
                {nameof(Menu.PermissionCode)} = :PermissionCode 
            WHERE {nameof(Menu.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new 
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
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $"DELETE FROM APP_MENU WHERE {nameof(Menu.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<Menu>> GetMenusByUserPermissionsAsync(IEnumerable<string> permissionCodes)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        // 1. Lấy toàn bộ các menu đang hoạt động trong hệ thống
        var sql = $@"
            SELECT {nameof(Menu.Id)}, 
                   {nameof(Menu.Name)}, 
                   {nameof(Menu.Url)}, 
                   {nameof(Menu.Icon)}, 
                   {nameof(Menu.ParentId)}, 
                   {nameof(Menu.SortOrder)}, 
                   {nameof(Menu.IsActive)}, 
                   {nameof(Menu.PermissionCode)} 
            FROM APP_MENU 
            WHERE {nameof(Menu.IsActive)} = 1";
        
        var allActiveMenus = (await _connection.QueryAsync<Menu>(sql)).ToList();

        var pList = new HashSet<string>(permissionCodes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        // 2. Xác định các menu được cấp quyền trực tiếp (hoặc không yêu cầu quyền)
        var allowedMenus = allActiveMenus
            .Where(m => string.IsNullOrEmpty(m.PermissionCode) || pList.Contains(m.PermissionCode))
            .ToDictionary(m => m.Id);

        // 3. Với mỗi menu con được phép truy cập, truy vết ngược lên để tự động hiển thị menu cha
        var resultDict = new Dictionary<long, Menu>(allowedMenus);
        var activeMenusById = allActiveMenus.ToDictionary(m => m.Id);

        foreach (var menu in allowedMenus.Values)
        {
            var current = menu;
            while (current.ParentId.HasValue)
            {
                if (activeMenusById.TryGetValue(current.ParentId.Value, out var parent))
                {
                    if (!resultDict.ContainsKey(parent.Id))
                    {
                        resultDict.Add(parent.Id, parent);
                    }
                    current = parent;
                }
                else
                {
                    break;
                }
            }
        }

        // 4. Sidebar chỉ hiển thị menu điều hướng: có URL hoặc là menu cha có ít nhất một con.
        // Menu không URL và không có con (vd. nhóm chỉ phục vụ phân quyền) vẫn giữ trong lookup/role UI.
        var sidebarMenus = FilterSidebarNavigationMenus(resultDict.Values);

        return sidebarMenus
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name);
    }

    /// <summary>
    /// Loại menu lá không URL khỏi sidebar. Giữ menu có URL hoặc menu cha (có menu con trong tập kết quả).
    /// </summary>
    private static List<Menu> FilterSidebarNavigationMenus(IEnumerable<Menu> menus)
    {
        var list = menus.ToList();
        var parentIdsWithChildren = list
            .Where(m => m.ParentId.HasValue)
            .Select(m => m.ParentId!.Value)
            .ToHashSet();

        return list
            .Where(m => !string.IsNullOrWhiteSpace(m.Url) || parentIdsWithChildren.Contains(m.Id))
            .ToList();
    }
}
