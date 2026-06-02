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
        // Nếu user có phân quyền, lấy các menu có PermissionCode nằm trong danh sách hoặc null.
        // Ngược lại chỉ lấy menu có PermissionCode null.
        var pList = new List<string>(permissionCodes ?? Array.Empty<string>());
        
        string sql;
        if (pList.Count > 0)
        {
            sql = $@"
                SELECT {nameof(Menu.Id)}, 
                       {nameof(Menu.Name)}, 
                       {nameof(Menu.Url)}, 
                       {nameof(Menu.Icon)}, 
                       {nameof(Menu.ParentId)}, 
                       {nameof(Menu.SortOrder)}, 
                       {nameof(Menu.IsActive)}, 
                       {nameof(Menu.PermissionCode)} 
                FROM APP_MENU 
                WHERE {nameof(Menu.IsActive)} = 1 AND ({nameof(Menu.PermissionCode)} IS NULL OR {nameof(Menu.PermissionCode)} IN :Permissions)
                ORDER BY {nameof(Menu.SortOrder)}, {nameof(Menu.Name)}";
            return await _connection.QueryAsync<Menu>(sql, new { Permissions = pList });
        }
        else
        {
            sql = $@"
                SELECT {nameof(Menu.Id)}, 
                       {nameof(Menu.Name)}, 
                       {nameof(Menu.Url)}, 
                       {nameof(Menu.Icon)}, 
                       {nameof(Menu.ParentId)}, 
                       {nameof(Menu.SortOrder)}, 
                       {nameof(Menu.IsActive)}, 
                       {nameof(Menu.PermissionCode)} 
                FROM APP_MENU 
                WHERE {nameof(Menu.IsActive)} = 1 AND {nameof(Menu.PermissionCode)} IS NULL
                ORDER BY {nameof(Menu.SortOrder)}, {nameof(Menu.Name)}";
            return await _connection.QueryAsync<Menu>(sql);
        }
    }
}
