using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
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

    public async Task<IEnumerable<Catalog>> GetAllAsync(
        long? catalogTypeId = null,
        string? keyword = null,
        int? status = null,
        long? unitId = null,
        string? username = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"SELECT * FROM {nameof(Catalog)}
                      WHERE {nameof(Catalog.IsDeleted)} = 0
                        AND ({nameof(Catalog.UnitId)} IS NULL";

        if (unitId.HasValue)
            sql += $" OR {nameof(Catalog.UnitId)} = :UnitId";
        sql += ")";

        if (catalogTypeId.HasValue)
            sql += $" AND {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId";

        if (!string.IsNullOrEmpty(username))
            sql += $" AND {nameof(Catalog.CreatedBy)} = :Username";

        if (!string.IsNullOrEmpty(keyword))
            sql += $" AND (LOWER({nameof(Catalog.Code)}) LIKE :Keyword OR LOWER({nameof(Catalog.Name)}) LIKE :Keyword)";

        if (status.HasValue)
            sql += $" AND {nameof(Catalog.Status)} = :Status";

        sql += $" ORDER BY {nameof(Catalog.Priority)} ASC, {nameof(Catalog.CreatedAt)} DESC";

        var keywordParam = !string.IsNullOrEmpty(keyword) ? $"%{keyword.ToLower()}%" : null;

        return await _connection.QueryAsync<Catalog>(sql, new
        {
            UnitId = unitId,
            CatalogTypeId = catalogTypeId,
            Keyword = keywordParam,
            Status = status,
            Username = username
        });
    }

    public async Task<(IEnumerable<Catalog> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        long? catalogTypeId = null,
        string? keyword = null,
        int? status = null,
        long? unitId = null,
        string? username = null,
        bool strictUnitFilter = false,
        bool includeAllUnits = false)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var filterSql = $" WHERE {nameof(Catalog.IsDeleted)} = 0";
        if (includeAllUnits)
        {
            // Không thêm điều kiện UnitId: quản trị viên được xem toàn bộ đơn vị.
        }
        else if (strictUnitFilter)
        {
            filterSql += $" AND {nameof(Catalog.UnitId)} = :UnitId";
        }
        else
        {
            filterSql += $" AND ({nameof(Catalog.UnitId)} IS NULL";
            if (unitId.HasValue)
                filterSql += $" OR {nameof(Catalog.UnitId)} = :UnitId";
            filterSql += ")";
        }

        if (catalogTypeId.HasValue)
            filterSql += $" AND {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId";

        if (!string.IsNullOrEmpty(username))
            filterSql += $" AND {nameof(Catalog.CreatedBy)} = :Username";

        if (!string.IsNullOrEmpty(keyword))
            filterSql += $" AND (LOWER({nameof(Catalog.Code)}) LIKE :Keyword OR LOWER({nameof(Catalog.Name)}) LIKE :Keyword)";

        if (status.HasValue)
            filterSql += $" AND {nameof(Catalog.Status)} = :Status";

        var countSql = $"SELECT COUNT(*) FROM {nameof(Catalog)}{filterSql}";

        var offset = (page - 1) * pageSize;
        var pagedSql = $@"
            SELECT pageData.*,
                   COALESCE(
                       creatorById.FullName,
                       creatorByUserName.FullName,
                       pageData.{nameof(Catalog.CreatedBy)}
                   ) AS {nameof(Catalog.CreatedByName)}
            FROM (
                SELECT c.*, ROW_NUMBER() OVER (ORDER BY c.{nameof(Catalog.Priority)} ASC, c.{nameof(Catalog.CreatedAt)} DESC) AS RN
                FROM {nameof(Catalog)} c
                {filterSql}
            ) pageData
            LEFT JOIN APP_USER creatorById
              ON creatorById.Id = pageData.{nameof(Catalog.CreatedBy)}
             AND creatorById.IsDeleted = 0
            LEFT JOIN APP_USER creatorByUserName
              ON UPPER(TRIM(creatorByUserName.UserName)) = UPPER(TRIM(pageData.{nameof(Catalog.CreatedBy)}))
             AND creatorByUserName.IsDeleted = 0
            WHERE pageData.RN > :Offset AND pageData.RN <= :OffsetPlusSize";

        var keywordParam = !string.IsNullOrEmpty(keyword) ? $"%{keyword.ToLower()}%" : null;
        var parameters = new DynamicParameters();
        parameters.Add("UnitId", unitId);
        parameters.Add("CatalogTypeId", catalogTypeId);
        parameters.Add("Keyword", keywordParam);
        parameters.Add("Status", status);
        parameters.Add("Username", username);
        parameters.Add("Offset", offset);
        parameters.Add("OffsetPlusSize", offset + pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await _connection.QueryAsync<Catalog>(pagedSql, parameters);

        return (items, totalCount);
    }

    public async Task<CatalogHierarchyPage> GetMucLucHierarchyPagedAsync(
        int page,
        int pageSize,
        long catalogTypeId,
        string? keyword = null,
        int? status = null,
        long? unitId = null,
        bool includeAllUnits = false)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        if (!includeAllUnits && !unitId.HasValue)
            return new CatalogHierarchyPage([], 0, 0);

        var unitFilter = includeAllUnits
            ? string.Empty
            : $" AND c.{nameof(Catalog.UnitId)} = :UnitId";
        var sql = $@"
            SELECT c.*,
                   ou.Name AS {nameof(Catalog.UnitName)},
                   COALESCE(
                       creatorById.FullName,
                       creatorByUserName.FullName,
                       c.{nameof(Catalog.CreatedBy)}
                   ) AS {nameof(Catalog.CreatedByName)}
              FROM {nameof(Catalog)} c
              LEFT JOIN ORGANIZATION_UNIT ou
                ON ou.Id = c.{nameof(Catalog.UnitId)}
              LEFT JOIN (
                    SELECT Id, MAX(FullName) AS FullName
                      FROM APP_USER
                     WHERE IsDeleted = 0
                     GROUP BY Id
              ) creatorById ON creatorById.Id = c.{nameof(Catalog.CreatedBy)}
              LEFT JOIN (
                    SELECT UPPER(TRIM(UserName)) AS NormalizedUserName,
                           MAX(FullName) AS FullName
                      FROM APP_USER
                     WHERE IsDeleted = 0
                     GROUP BY UPPER(TRIM(UserName))
              ) creatorByUserName
                ON creatorByUserName.NormalizedUserName = UPPER(TRIM(c.{nameof(Catalog.CreatedBy)}))
             WHERE c.{nameof(Catalog.IsDeleted)} = 0
               AND c.{nameof(Catalog.CatalogTypeId)} = :CatalogTypeId
               {unitFilter}";

        var source = (await _connection.QueryAsync<CatalogHierarchyItemDto>(sql, new
        {
            CatalogTypeId = catalogTypeId,
            UnitId = unitId
        })).ToList();

        if (source.Count == 0)
            return new CatalogHierarchyPage([], 0, 0);

        static int CompareItems(CatalogHierarchyItemDto left, CatalogHierarchyItemDto right)
        {
            var priorityComparison = left.Priority.CompareTo(right.Priority);
            return priorityComparison != 0 ? priorityComparison : left.Id.CompareTo(right.Id);
        }

        var itemsById = source.ToDictionary(item => item.Id);
        var childrenByParent = source
            .Where(item => item.ParentId.HasValue &&
                           itemsById.TryGetValue(item.ParentId.Value, out var parent) &&
                           parent.UnitId == item.UnitId)
            .GroupBy(item => item.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item, Comparer<CatalogHierarchyItemDto>.Create(CompareItems)).ToList());

        var normalizedKeyword = keyword?.Trim();
        var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
        var hasFilter = hasKeyword || status.HasValue;
        bool IsDirectMatch(CatalogHierarchyItemDto item) =>
            (!hasKeyword ||
             item.Code.Contains(normalizedKeyword!, StringComparison.OrdinalIgnoreCase) ||
             item.Name.Contains(normalizedKeyword!, StringComparison.OrdinalIgnoreCase)) &&
            (!status.HasValue || item.Status == status.Value);

        var directMatchIds = source.Where(IsDirectMatch).Select(item => item.Id).ToHashSet();
        var visibleIds = hasFilter ? new HashSet<long>() : source.Select(item => item.Id).ToHashSet();

        if (hasFilter)
        {
            foreach (var matchId in directMatchIds)
            {
                var currentId = (long?)matchId;
                var visited = new HashSet<long>();
                while (currentId.HasValue && visited.Add(currentId.Value) &&
                       itemsById.TryGetValue(currentId.Value, out var current))
                {
                    visibleIds.Add(current.Id);
                    if (!current.ParentId.HasValue ||
                        !itemsById.TryGetValue(current.ParentId.Value, out var parent) ||
                        parent.UnitId != current.UnitId)
                        break;
                    currentId = parent.Id;
                }
            }
        }

        if (visibleIds.Count == 0)
            return new CatalogHierarchyPage([], 0, 0);

        var roots = source
            .Where(item => visibleIds.Contains(item.Id) &&
                           (!item.ParentId.HasValue ||
                            !visibleIds.Contains(item.ParentId.Value) ||
                            !itemsById.TryGetValue(item.ParentId.Value, out var parent) ||
                            parent.UnitId != item.UnitId))
            .ToList();

        var reachableIds = new HashSet<long>();
        void MarkReachable(long id, HashSet<long> path)
        {
            if (!visibleIds.Contains(id) || !path.Add(id)) return;
            reachableIds.Add(id);
            if (childrenByParent.TryGetValue(id, out var children))
                foreach (var child in children)
                    MarkReachable(child.Id, path);
            path.Remove(id);
        }

        foreach (var root in roots)
            MarkReachable(root.Id, []);

        // Keep malformed/orphaned cycle components visible instead of silently dropping them.
        // Select one synthetic root per disconnected component so pagination stays stable.
        foreach (var candidate in source
                     .Where(item => visibleIds.Contains(item.Id))
                     .OrderBy(item => item.Id))
        {
            if (reachableIds.Contains(candidate.Id)) continue;
            roots.Add(candidate);
            MarkReachable(candidate.Id, []);
        }
        roots = roots
            .DistinctBy(item => item.Id)
            .OrderBy(item => includeAllUnits ? item.UnitName ?? string.Empty : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => includeAllUnits ? item.UnitId ?? 0 : 0)
            .ThenBy(item => item, Comparer<CatalogHierarchyItemDto>.Create(CompareItems))
            .ToList();

        var totalRootCount = roots.Count;
        var pageRoots = roots.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var pageItems = new List<CatalogHierarchyItemDto>();
        var emittedIds = new HashSet<long>();

        void Flatten(CatalogHierarchyItemDto item, int level, HashSet<long> path)
        {
            if (!visibleIds.Contains(item.Id) || !path.Add(item.Id) || !emittedIds.Add(item.Id)) return;

            var visibleChildren = childrenByParent.TryGetValue(item.Id, out var children)
                ? children.Where(child => visibleIds.Contains(child.Id)).ToList()
                : [];
            item.Level = level;
            item.HasChildren = visibleChildren.Count > 0;
            item.IsContextOnly = hasFilter && !directMatchIds.Contains(item.Id);
            pageItems.Add(item);

            foreach (var child in visibleChildren)
                Flatten(child, level + 1, path);
            path.Remove(item.Id);
        }

        foreach (var root in pageRoots)
            Flatten(root, 0, []);

        return new CatalogHierarchyPage(pageItems, totalRootCount, visibleIds.Count);
    }

    public async Task<(IEnumerable<Catalog> Items, int TotalCount)> GetPhongPagedAsync(
        int page, int pageSize, long catalogTypeId, long? unitId,
        string? name = null, string? code = null, int? status = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var filterSql = $@" WHERE {nameof(Catalog.IsDeleted)} = 0
                            AND {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId";
        if (unitId.HasValue)
            filterSql += $" AND {nameof(Catalog.UnitId)} = :UnitId";
        if (!string.IsNullOrWhiteSpace(name))
            filterSql += $" AND LOWER({nameof(Catalog.Name)}) LIKE :Name";
        if (!string.IsNullOrWhiteSpace(code))
            filterSql += $" AND LOWER({nameof(Catalog.Code)}) LIKE :Code";
        if (status.HasValue)
            filterSql += $" AND {nameof(Catalog.Status)} = :Status";

        var parameters = new DynamicParameters();
        parameters.Add("CatalogTypeId", catalogTypeId);
        parameters.Add("UnitId", unitId);
        parameters.Add("Name", string.IsNullOrWhiteSpace(name) ? null : $"%{name.Trim().ToLower()}%");
        parameters.Add("Code", string.IsNullOrWhiteSpace(code) ? null : $"%{code.Trim().ToLower()}%");
        parameters.Add("Status", status);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("OffsetPlusSize", page * pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {nameof(Catalog)}{filterSql}", parameters);
        var items = await _connection.QueryAsync<Catalog>($@"
            SELECT * FROM (
                SELECT c.*, ROW_NUMBER() OVER (ORDER BY c.{nameof(Catalog.Priority)}, c.{nameof(Catalog.CreatedAt)} DESC) RN
                  FROM {nameof(Catalog)} c {filterSql}
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize", parameters);
        return (items, totalCount);
    }

    public async Task<Catalog?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $"SELECT * FROM {nameof(Catalog)} WHERE {nameof(Catalog.Id)} = :Id AND {nameof(Catalog.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<Catalog>(sql, new { Id = id });
    }

    public async Task<Catalog?> GetByCodeAsync(long catalogTypeId, string code)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"SELECT * FROM {nameof(Catalog)}
                     WHERE {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId
                       AND LOWER({nameof(Catalog.Code)}) = LOWER(:Code)
                       AND {nameof(Catalog.IsDeleted)} = 0";
        return await _connection.QueryFirstOrDefaultAsync<Catalog>(sql, new { CatalogTypeId = catalogTypeId, Code = code });
    }

    public async Task<Catalog?> GetByCodeForUnitAsync(long catalogTypeId, string code, long unitId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"SELECT * FROM {nameof(Catalog)}
                     WHERE {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId
                       AND LOWER({nameof(Catalog.Code)}) = LOWER(:Code)
                       AND {nameof(Catalog.UnitId)} = :UnitId
                       AND {nameof(Catalog.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<Catalog>(sql, new { CatalogTypeId = catalogTypeId, Code = code, UnitId = unitId });
    }

    public async Task<Catalog?> GetByCodeIncludingDeletedAsync(long catalogTypeId, string code)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"SELECT * FROM {nameof(Catalog)}
                     WHERE {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId
                       AND {nameof(Catalog.Code)} = :Code";
        return await _connection.QuerySingleOrDefaultAsync<Catalog>(sql, new { CatalogTypeId = catalogTypeId, Code = code });
    }

    public async Task<Catalog?> GetDeletedByCodeAsync(
        long catalogTypeId,
        string code,
        long? unitId = null,
        bool strictUnitFilter = false)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"SELECT * FROM {nameof(Catalog)}
                     WHERE {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId
                       AND LOWER({nameof(Catalog.Code)}) = LOWER(:Code)
                       AND {nameof(Catalog.IsDeleted)} = 1";
        if (strictUnitFilter)
            sql += $" AND {nameof(Catalog.UnitId)} = :UnitId";

        return await _connection.QueryFirstOrDefaultAsync<Catalog>(
            sql,
            new { CatalogTypeId = catalogTypeId, Code = code, UnitId = unitId });
    }

    public async Task<bool> RestoreAsync(Catalog catalog)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"UPDATE {nameof(Catalog)}
                     SET {nameof(Catalog.IsDeleted)} = 0,
                         {nameof(Catalog.Code)} = :Code,
                         {nameof(Catalog.Name)} = :Name,
                         {nameof(Catalog.ParentId)} = :ParentId,
                         {nameof(Catalog.Description)} = :Description,
                         {nameof(Catalog.UnitId)} = :UnitId,
                         {nameof(Catalog.Priority)} = :Priority,
                         {nameof(Catalog.Status)} = :Status,
                         {nameof(Catalog.UpdatedAt)} = CURRENT_TIMESTAMP,
                         {nameof(Catalog.UpdatedBy)} = :UpdatedBy
                     WHERE {nameof(Catalog.Id)} = :Id
                       AND {nameof(Catalog.IsDeleted)} = 1";
        var affected = await _connection.ExecuteAsync(sql, catalog);
        return affected == 1;
    }

    public async Task<bool> HasChildrenAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $"SELECT COUNT(1) FROM {nameof(Catalog)} WHERE {nameof(Catalog.ParentId)} = :Id AND {nameof(Catalog.IsDeleted)} = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return count > 0;
    }

    public async Task<long> CreateAsync(Catalog catalog)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"
            INSERT INTO {nameof(Catalog)} (
                {nameof(Catalog.Code)},
                {nameof(Catalog.Name)},
                {nameof(Catalog.CatalogTypeId)},
                {nameof(Catalog.ParentId)},
                {nameof(Catalog.Description)},
                {nameof(Catalog.UnitId)},
                {nameof(Catalog.CreatedBy)},
                {nameof(Catalog.Priority)},
                {nameof(Catalog.Status)}
            )
            VALUES (:Code, :Name, :CatalogTypeId, :ParentId, :Description, :UnitId, :CreatedBy, :Priority, :Status)
            RETURNING {nameof(Catalog.Id)} INTO :Id";

        var parameters = new DynamicParameters(catalog);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Catalog catalog)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"
            UPDATE {nameof(Catalog)} SET
                {nameof(Catalog.Code)}          = :Code,
                {nameof(Catalog.Name)}          = :Name,
                {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId,
                {nameof(Catalog.ParentId)}      = :ParentId,
                {nameof(Catalog.Description)}   = :Description,
                {nameof(Catalog.UnitId)}        = :UnitId,
                {nameof(Catalog.Priority)}      = :Priority,
                {nameof(Catalog.Status)}        = :Status,
                {nameof(Catalog.UpdatedAt)}     = CURRENT_TIMESTAMP,
                {nameof(Catalog.UpdatedBy)}     = :UpdatedBy
            WHERE {nameof(Catalog.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, catalog);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id, string updatedBy)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"UPDATE {nameof(Catalog)} 
                     SET {nameof(Catalog.IsDeleted)} = 1,
                         {nameof(Catalog.UpdatedAt)} = CURRENT_TIMESTAMP,
                         {nameof(Catalog.UpdatedBy)} = :UpdatedBy
                     WHERE {nameof(Catalog.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id, UpdatedBy = updatedBy });
        return affected > 0;
    }

    public async Task<IEnumerable<CatalogType>> GetCatalogTypesAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $"SELECT * FROM CATALOG_TYPE WHERE {nameof(CatalogType.IsDeleted)} = 0 ORDER BY {nameof(CatalogType.Name)} ASC";
        return await _connection.QueryAsync<CatalogType>(sql);
    }

    public async Task<CatalogType?> GetCatalogTypeByCodeAsync(string code)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $"SELECT * FROM CATALOG_TYPE WHERE {nameof(CatalogType.Code)} = :Code AND {nameof(CatalogType.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<CatalogType>(sql, new { Code = code });
    }

    public async Task<CatalogType?> GetCatalogTypeByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $"SELECT * FROM CATALOG_TYPE WHERE {nameof(CatalogType.Id)} = :Id AND {nameof(CatalogType.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<CatalogType>(sql, new { Id = id });
    }

    public async Task<IEnumerable<CatalogType>> GetCatalogTypesFilteredAsync(bool isPrivate, string? keyword = null, int? status = null, string? username = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"SELECT * FROM CATALOG_TYPE 
                     WHERE {nameof(CatalogType.IsDeleted)} = 0
                       AND {nameof(CatalogType.IsPrivate)} = :IsPrivate";

        if (isPrivate && !string.IsNullOrEmpty(username))
        {
            sql += $" AND {nameof(CatalogType.CreatedBy)} = :Username";
        }

        if (!string.IsNullOrEmpty(keyword))
            sql += $" AND LOWER({nameof(CatalogType.Name)}) LIKE :Keyword";

        if (status.HasValue)
            sql += $" AND {nameof(CatalogType.Status)} = :Status";

        sql += $" ORDER BY {nameof(CatalogType.Status)} DESC, {nameof(CatalogType.CreatedAt)} DESC";

        var keywordParam = !string.IsNullOrEmpty(keyword) ? $"%{keyword.ToLower()}%" : null;

        return await _connection.QueryAsync<CatalogType>(sql, new
        {
            IsPrivate = isPrivate ? 1 : 0,
            Keyword = keywordParam,
            Status = status,
            Username = username
        });
    }

    public async Task<CatalogType?> GetCatalogTypeByIdFilteredAsync(long id, bool isPrivate, string? username = null)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"SELECT * FROM CATALOG_TYPE 
                     WHERE {nameof(CatalogType.Id)} = :Id 
                       AND {nameof(CatalogType.IsDeleted)} = 0";
        sql += isPrivate
            ? $" AND ({nameof(CatalogType.IsPrivate)} = 1 OR {nameof(CatalogType.Code)} IN ('PHONG', 'MUC_LUC'))"
            : $" AND {nameof(CatalogType.IsPrivate)} = 0";

        if (isPrivate && !string.IsNullOrEmpty(username))
        {
            sql += $" AND ({nameof(CatalogType.CreatedBy)} = :Username OR {nameof(CatalogType.Code)} IN ('PHONG', 'MUC_LUC'))";
        }

        return await _connection.QuerySingleOrDefaultAsync<CatalogType>(sql, new { Id = id, IsPrivate = isPrivate ? 1 : 0, Username = username });
    }

    public async Task<long> CreateCatalogTypeAsync(CatalogType catalogType)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"
            INSERT INTO CATALOG_TYPE (
                {nameof(CatalogType.Id)},
                {nameof(CatalogType.Code)},
                {nameof(CatalogType.Name)},
                {nameof(CatalogType.HasParent)},
                {nameof(CatalogType.Description)},
                {nameof(CatalogType.IsPrivate)},
                {nameof(CatalogType.Status)},
                {nameof(CatalogType.CreatedBy)}
            )
            VALUES (SEQ_CATALOG_TYPE_ID.NEXTVAL, :Code, :Name, :HasParent, :Description, :IsPrivate, :Status, :CreatedBy)
            RETURNING {nameof(CatalogType.Id)} INTO :Id";

        var parameters = new DynamicParameters();
        parameters.Add("Code", catalogType.Code);
        parameters.Add("Name", catalogType.Name);
        parameters.Add("HasParent", catalogType.HasParent);
        parameters.Add("Description", catalogType.Description);
        parameters.Add("IsPrivate", catalogType.IsPrivate ? 1 : 0);
        parameters.Add("Status", catalogType.Status);
        parameters.Add("CreatedBy", catalogType.CreatedBy ?? "system");
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateCatalogTypeAsync(CatalogType catalogType)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $@"
            UPDATE CATALOG_TYPE SET
                {nameof(CatalogType.Code)}        = :Code,
                {nameof(CatalogType.Name)}        = :Name,
                {nameof(CatalogType.HasParent)}   = :HasParent,
                {nameof(CatalogType.Description)} = :Description,
                {nameof(CatalogType.IsPrivate)}   = :IsPrivate,
                {nameof(CatalogType.Status)}      = :Status
            WHERE {nameof(CatalogType.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            catalogType.Code,
            catalogType.Name,
            catalogType.HasParent,
            catalogType.Description,
            IsPrivate = catalogType.IsPrivate ? 1 : 0,
            catalogType.Status,
            catalogType.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteCatalogTypeAsync(long id, string updatedBy)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        using var transaction = _connection.BeginTransaction();
        try
        {
            var sql = $@"UPDATE CATALOG_TYPE
                         SET {nameof(CatalogType.IsDeleted)} = 1,
                             {nameof(CatalogType.UpdatedAt)} = CURRENT_TIMESTAMP,
                             {nameof(CatalogType.UpdatedBy)} = :UpdatedBy
                         WHERE {nameof(CatalogType.Id)} = :Id
                           AND {nameof(CatalogType.IsDeleted)} = 0";
            var affected = await _connection.ExecuteAsync(
                sql,
                new { Id = id, UpdatedBy = updatedBy },
                transaction);

            if (affected > 0)
            {
                var sqlCatalogs = $@"UPDATE {nameof(Catalog)}
                                     SET {nameof(Catalog.IsDeleted)} = 1,
                                         {nameof(Catalog.UpdatedAt)} = CURRENT_TIMESTAMP,
                                         {nameof(Catalog.UpdatedBy)} = :UpdatedBy
                                     WHERE {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId
                                       AND {nameof(Catalog.IsDeleted)} = 0";
                await _connection.ExecuteAsync(
                    sqlCatalogs,
                    new { CatalogTypeId = id, UpdatedBy = updatedBy },
                    transaction);
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> CatalogTypeHasCatalogsAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = $"SELECT COUNT(1) FROM CATALOG WHERE {nameof(Catalog.CatalogTypeId)} = :CatalogTypeId AND {nameof(Catalog.IsDeleted)} = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { CatalogTypeId = id });
        return count > 0;
    }
}
