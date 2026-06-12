using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly IDbConnection _connection;

    public CatalogRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<IEnumerable<Catalog>> GetAllAsync(string? catalogType = null, string? keyword = null, int? status = null, long? unitId = null)
    {
        var sql = $@"SELECT * FROM {nameof(Catalog)} 
                     WHERE ({nameof(Catalog.UnitId)} IS NULL";
        
        if (unitId.HasValue)
        {
            sql += $" OR {nameof(Catalog.UnitId)} = :UnitId";
        }
        sql += ")";

        if (!string.IsNullOrEmpty(catalogType))
        {
            sql += $" AND {nameof(Catalog.CatalogType)} = :CatalogType";
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            sql += $" AND (LOWER({nameof(Catalog.Code)}) LIKE :Keyword OR LOWER({nameof(Catalog.Name)}) LIKE :Keyword)";
        }

        if (status.HasValue)
        {
            sql += $" AND {nameof(Catalog.Status)} = :Status";
        }

        sql += $" ORDER BY {nameof(Catalog.Priority)} ASC, {nameof(Catalog.CreatedAt)} DESC";
        
        var keywordParam = !string.IsNullOrEmpty(keyword) ? $"%{keyword.ToLower()}%" : null;

        return await _connection.QueryAsync<Catalog>(sql, new { 
            UnitId = unitId, 
            CatalogType = catalogType, 
            Keyword = keywordParam, 
            Status = status 
        });
    }

    public async Task<(IEnumerable<Catalog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? catalogType = null, string? keyword = null, int? status = null, long? unitId = null)
    {
        var filterSql = $" WHERE ({nameof(Catalog.UnitId)} IS NULL";
        if (unitId.HasValue)
        {
            filterSql += $" OR {nameof(Catalog.UnitId)} = :UnitId";
        }
        filterSql += ")";

        if (!string.IsNullOrEmpty(catalogType))
        {
            filterSql += $" AND {nameof(Catalog.CatalogType)} = :CatalogType";
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            filterSql += $" AND (LOWER({nameof(Catalog.Code)}) LIKE :Keyword OR LOWER({nameof(Catalog.Name)}) LIKE :Keyword)";
        }

        if (status.HasValue)
        {
            filterSql += $" AND {nameof(Catalog.Status)} = :Status";
        }

        var countSql = $"SELECT COUNT(*) FROM {nameof(Catalog)} {filterSql}";
        
        var offset = (page - 1) * pageSize;
        var pagedSql = $@"
            SELECT * FROM (
                SELECT c.*, ROW_NUMBER() OVER (ORDER BY c.Priority ASC, c.CreatedAt DESC) AS RN
                FROM {nameof(Catalog)} c
                {filterSql}
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";

        var keywordParam = !string.IsNullOrEmpty(keyword) ? $"%{keyword.ToLower()}%" : null;
        var parameters = new DynamicParameters();
        parameters.Add("UnitId", unitId);
        parameters.Add("CatalogType", catalogType);
        parameters.Add("Keyword", keywordParam);
        parameters.Add("Status", status);
        parameters.Add("Offset", offset);
        parameters.Add("OffsetPlusSize", offset + pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await _connection.QueryAsync<Catalog>(pagedSql, parameters);

        return (items, totalCount);
    }

    public async Task<Catalog?> GetByIdAsync(long id)
    {
        var sql = $"SELECT * FROM {nameof(Catalog)} WHERE {nameof(Catalog.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<Catalog>(sql, new { Id = id });
    }

    public async Task<Catalog?> GetByCodeAsync(string catalogType, string code)
    {
        var sql = $"SELECT * FROM {nameof(Catalog)} WHERE {nameof(Catalog.CatalogType)} = :CatalogType AND {nameof(Catalog.Code)} = :Code";
        return await _connection.QuerySingleOrDefaultAsync<Catalog>(sql, new { CatalogType = catalogType, Code = code });
    }

    public async Task<bool> HasChildrenAsync(long id)
    {
        var sql = $"SELECT COUNT(1) FROM {nameof(Catalog)} WHERE {nameof(Catalog.ParentId)} = :Id";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return count > 0;
    }

    public async Task<long> CreateAsync(Catalog catalog)
    {
        var sql = $@"
            INSERT INTO {nameof(Catalog)} (
                {nameof(Catalog.Code)}, 
                {nameof(Catalog.Name)}, 
                {nameof(Catalog.CatalogType)}, 
                {nameof(Catalog.ParentId)}, 
                {nameof(Catalog.Description)}, 
                {nameof(Catalog.UnitId)}, 
                {nameof(Catalog.CreatedBy)},
                Priority,
                Status
            )
            VALUES (:Code, :Name, :CatalogType, :ParentId, :Description, :UnitId, :CreatedBy, :Priority, :Status)
            RETURNING {nameof(Catalog.Id)} INTO :Id";
            
        var parameters = new DynamicParameters(catalog);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Catalog catalog)
    {
        var sql = $@"
            UPDATE {nameof(Catalog)} SET 
                {nameof(Catalog.Code)} = :Code, 
                {nameof(Catalog.Name)} = :Name, 
                {nameof(Catalog.CatalogType)} = :CatalogType, 
                {nameof(Catalog.ParentId)} = :ParentId, 
                {nameof(Catalog.Description)} = :Description, 
                {nameof(Catalog.UnitId)} = :UnitId, 
                Priority = :Priority,
                Status = :Status,
                {nameof(Catalog.UpdatedAt)} = CURRENT_TIMESTAMP, 
                {nameof(Catalog.UpdatedBy)} = :UpdatedBy
            WHERE {nameof(Catalog.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, catalog);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var sql = $"DELETE FROM {nameof(Catalog)} WHERE {nameof(Catalog.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<CatalogType>> GetCatalogTypesAsync()
    {
        var sql = $"SELECT * FROM CATALOG_TYPE ORDER BY {nameof(CatalogType.Name)} ASC";
        return await _connection.QueryAsync<CatalogType>(sql);
    }

    public async Task<CatalogType?> GetCatalogTypeByCodeAsync(string code)
    {
        var sql = $"SELECT * FROM CATALOG_TYPE WHERE {nameof(CatalogType.Code)} = :Code";
        return await _connection.QuerySingleOrDefaultAsync<CatalogType>(sql, new { Code = code });
    }
}
