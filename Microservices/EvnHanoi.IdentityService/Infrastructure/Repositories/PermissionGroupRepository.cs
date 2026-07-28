using System;

using System.Collections.Generic;

using System.Data;

using System.Linq;

using System.Threading.Tasks;

using Dapper;

using EvnHanoi.IdentityService.Core.Domain.Models;

using EvnHanoi.IdentityService.Core.Interfaces;



namespace EvnHanoi.IdentityService.Infrastructure.Repositories;



public class PermissionGroupRepository : IPermissionGroupRepository

{

    private readonly IDbConnection _connection;

    private readonly IPermissionRepository _permissionRepository;



    public PermissionGroupRepository(IDbConnection connection, IPermissionRepository permissionRepository)

    {

        _connection = connection;

        _permissionRepository = permissionRepository;

    }



    public async Task<IEnumerable<PermissionGroup>> GetAllAsync(string groupType, long? organizationUnitId = null)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();



        var sql = BuildSelectSql("WHERE st.Code = :GroupType");

        var parameters = new DynamicParameters();

        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;

        parameters.Add("GroupType", dbGroupType);



        if (organizationUnitId.HasValue)

        {

            sql += @" AND EXISTS (

                SELECT 1 FROM PERMISSION_GROUP_UNIT pgu

                WHERE pgu.PermissionGroupId = pg.Id AND pgu.OrganizationUnitId = :OrganizationUnitId

            )";

            parameters.Add("OrganizationUnitId", organizationUnitId.Value);

        }



        sql += " ORDER BY pg.Id";

        var items = (await _connection.QueryAsync<PermissionGroup>(sql, parameters)).ToList();

        await HydrateOrganizationUnitsAsync(items);

        return items;

    }



    public async Task<(IEnumerable<PermissionGroup> Items, int TotalCount)> GetPagedAsync(

        string groupType, int page, int pageSize, string? keyword = null, long? organizationUnitId = null)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();



        var conditions = new List<string> { "st.Code = :GroupType" };

        var parameters = new DynamicParameters();

        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;

        parameters.Add("GroupType", dbGroupType);



        if (!string.IsNullOrWhiteSpace(keyword))

        {

            conditions.Add("(UPPER(pg.Code) LIKE UPPER(:Keyword) OR UPPER(pg.Name) LIKE UPPER(:Keyword) OR UPPER(pg.Description) LIKE UPPER(:Keyword))");

            parameters.Add("Keyword", $"%{keyword.Trim()}%");

        }



        if (organizationUnitId.HasValue)

        {

            conditions.Add(@"EXISTS (

                SELECT 1 FROM PERMISSION_GROUP_UNIT pgu

                WHERE pgu.PermissionGroupId = pg.Id AND pgu.OrganizationUnitId = :OrganizationUnitId

            )");

            parameters.Add("OrganizationUnitId", organizationUnitId.Value);

        }



        var whereClause = "WHERE " + string.Join(" AND ", conditions);

        var countSql = $"SELECT COUNT(*) FROM PERMISSION_GROUP pg INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id {whereClause}";

        var offset = (page - 1) * pageSize;



        var sql = $@"

            SELECT * FROM (

                SELECT pg.Id, pg.Code, pg.Name, pg.Description, pg.ScopeTypeId,

                       CASE WHEN st.Code = 'GLOBAL' THEN 'SYSTEM' ELSE st.Code END AS GroupType,

                       st.Name AS ScopeTypeName, pg.OrganizationUnitId,

                       o.Name AS OrganizationUnitName,

                       (SELECT LISTAGG(ou.Name, ', ') WITHIN GROUP (ORDER BY ou.Name)

                          FROM PERMISSION_GROUP_UNIT pgu2

                          INNER JOIN ORGANIZATION_UNIT ou ON ou.Id = pgu2.OrganizationUnitId

                         WHERE pgu2.PermissionGroupId = pg.Id) AS OrganizationUnitNames,

                       pg.CreatedAt,

                       pg.CreatedBy,

                       creator.FullName AS CreatedByName,

                       pg.IsActive,

                       ROW_NUMBER() OVER (ORDER BY pg.Id ASC) AS RN

                FROM PERMISSION_GROUP pg

                INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id

                LEFT JOIN ORGANIZATION_UNIT o ON pg.OrganizationUnitId = o.Id

                LEFT JOIN APP_USER creator ON creator.Id = pg.CreatedBy

                {whereClause}

            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";



        parameters.Add("Offset", offset);

        parameters.Add("OffsetPlusSize", offset + pageSize);



        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var items = (await _connection.QueryAsync<PermissionGroup>(sql, parameters)).ToList();

        await HydrateOrganizationUnitsAsync(items);

        return (items, totalCount);

    }



    public async Task<PermissionGroup?> GetByIdAsync(long id, string groupType)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = BuildSelectSql("WHERE pg.Id = :Id AND st.Code = :GroupType");

        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;

        var result = await _connection.QuerySingleOrDefaultAsync<PermissionGroup>(sql, new { Id = id, GroupType = dbGroupType });

        if (result != null)

        {

            await HydrateOrganizationUnitsAsync(new[] { result });

        }

        return result;

    }



    public async Task<long> CreateAsync(PermissionGroup group)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();



        var unitIds = NormalizeUnitIds(group);

        group.OrganizationUnitId = unitIds.Count > 0 ? unitIds[0] : null;



        using var transaction = _connection.BeginTransaction();

        try

        {

            var sql = @"

                INSERT INTO PERMISSION_GROUP (Code, Name, Description, ScopeTypeId, OrganizationUnitId, IsActive, CreatedBy)

                VALUES (:Code, :Name, :Description, :ScopeTypeId, :OrganizationUnitId, :IsActive, :CreatedBy)

                RETURNING Id INTO :Id";



            var scopeTypeId = ResolveScopeTypeId(group);



            var parameters = new DynamicParameters();

            parameters.Add("Code", group.Code);

            parameters.Add("Name", group.Name);

            parameters.Add("Description", group.Description);

            parameters.Add("ScopeTypeId", scopeTypeId);

            parameters.Add("OrganizationUnitId", group.OrganizationUnitId);

            parameters.Add("IsActive", group.IsActive ? 1 : 0);
            parameters.Add("CreatedBy", group.CreatedBy);

            parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);



            await _connection.ExecuteAsync(sql, parameters, transaction);

            var newId = parameters.Get<long>("Id");



            await ReplaceOrganizationUnitsAsync(newId, unitIds, transaction);

            transaction.Commit();

            return newId;

        }

        catch

        {

            transaction.Rollback();

            throw;

        }

    }



    public async Task<bool> UpdateAsync(PermissionGroup group)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();



        var unitIds = NormalizeUnitIds(group);

        group.OrganizationUnitId = unitIds.Count > 0 ? unitIds[0] : null;



        using var transaction = _connection.BeginTransaction();

        try

        {

            var sql = @"

                UPDATE PERMISSION_GROUP

                SET Code = :Code, Name = :Name, Description = :Description,

                    OrganizationUnitId = :OrganizationUnitId, IsActive = :IsActive, UpdatedAt = CURRENT_TIMESTAMP

                WHERE Id = :Id AND ScopeTypeId = :ScopeTypeId";



            var scopeTypeId = ResolveScopeTypeId(group);



            var affected = await _connection.ExecuteAsync(sql, new

            {

                group.Code,

                group.Name,

                group.Description,

                group.OrganizationUnitId,

                IsActive = group.IsActive ? 1 : 0,

                group.Id,

                ScopeTypeId = scopeTypeId

            }, transaction);



            if (affected > 0 && string.Equals(group.GroupType, PermissionGroupTypes.Unit, StringComparison.OrdinalIgnoreCase))

            {

                await ReplaceOrganizationUnitsAsync(group.Id, unitIds, transaction);

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



    public async Task<bool> DeleteAsync(long id, string groupType)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sql = @"

            DELETE FROM PERMISSION_GROUP 

            WHERE Id = :Id 

              AND ScopeTypeId = (SELECT Id FROM SCOPE_TYPE WHERE Code = :GroupType)";

        var dbGroupType = string.Equals(groupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : groupType;

        var affected = await _connection.ExecuteAsync(sql, new { Id = id, GroupType = dbGroupType });

        return affected > 0;

    }



    public async Task<IEnumerable<string>> GetPermissionCodesByGroupIdAsync(long permissionGroupId)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"

            SELECT p.Code

            FROM PERMISSION_GROUP_PERMISSION pgp

            INNER JOIN PERMISSION p ON pgp.PermissionId = p.Id

            WHERE pgp.PermissionGroupId = :PermissionGroupId";

        return await _connection.QueryAsync<string>(sql, new { PermissionGroupId = permissionGroupId });

    }

    public async Task<IReadOnlyDictionary<long, IReadOnlyCollection<string>>> GetPermissionCodesByGroupIdsAsync(
        IEnumerable<long> permissionGroupIds)
    {
        var ids = permissionGroupIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<long, IReadOnlyCollection<string>>();
        }

        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"
            SELECT pgp.PermissionGroupId, p.Code AS PermissionCode
            FROM PERMISSION_GROUP_PERMISSION pgp
            INNER JOIN PERMISSION p ON pgp.PermissionId = p.Id
            WHERE pgp.PermissionGroupId IN :PermissionGroupIds
              AND p.IsActive = 1
            ORDER BY pgp.PermissionGroupId, p.Code";

        var rows = await _connection.QueryAsync<PermissionGroupCodeRow>(
            sql,
            new { PermissionGroupIds = ids });

        return rows
            .GroupBy(row => row.PermissionGroupId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group
                    .Select(row => row.PermissionCode)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
    }

    private sealed class PermissionGroupCodeRow
    {
        public long PermissionGroupId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
    }



    public async Task<bool> AssignPermissionsToGroupAsync(long permissionGroupId, IEnumerable<string> permissionCodes)

    {

        var permissions = await _permissionRepository.GetAllPermissionsAsync();

        var codeToIdMap = permissions

            .Where(p => p.IsActive)

            .ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);



        if (_connection.State != ConnectionState.Open) _connection.Open();

        using var transaction = _connection.BeginTransaction();

        try

        {

            await _connection.ExecuteAsync(

                "DELETE FROM PERMISSION_GROUP_PERMISSION WHERE PermissionGroupId = :PermissionGroupId",

                new { PermissionGroupId = permissionGroupId },

                transaction);



            const string sql = @"

                INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)

                VALUES (:Id, :PermissionGroupId, :PermissionId)";



            foreach (var code in permissionCodes)

            {

                if (codeToIdMap.TryGetValue(code, out var permissionId))

                {

                    await _connection.ExecuteAsync(sql, new

                    {

                        Id = Guid.NewGuid().ToString(),

                        PermissionGroupId = permissionGroupId,

                        PermissionId = permissionId

                    }, transaction);

                }

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



    public async Task<IEnumerable<long>> GetPermissionGroupIdsByRoleIdAsync(long roleId)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = "SELECT PermissionGroupId FROM ROLE_PERMISSION_GROUP WHERE RoleId = :RoleId";

        return await _connection.QueryAsync<long>(sql, new { RoleId = roleId });

    }



    public async Task<IEnumerable<PermissionGroup>> GetPermissionGroupsByRoleIdAsync(long roleId)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"

            SELECT pg.Id, pg.Code, pg.Name, pg.Description, pg.ScopeTypeId,

                   CASE WHEN st.Code = 'GLOBAL' THEN 'SYSTEM' ELSE st.Code END AS GroupType,

                   st.Name AS ScopeTypeName, pg.OrganizationUnitId,

                   o.Name AS OrganizationUnitName,

                   (SELECT LISTAGG(ou.Name, ', ') WITHIN GROUP (ORDER BY ou.Name)

                      FROM PERMISSION_GROUP_UNIT pgu2

                      INNER JOIN ORGANIZATION_UNIT ou ON ou.Id = pgu2.OrganizationUnitId

                     WHERE pgu2.PermissionGroupId = pg.Id) AS OrganizationUnitNames,

                   pg.IsActive

            FROM ROLE_PERMISSION_GROUP rpg

            INNER JOIN PERMISSION_GROUP pg ON rpg.PermissionGroupId = pg.Id

            INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id

            LEFT JOIN ORGANIZATION_UNIT o ON pg.OrganizationUnitId = o.Id

            WHERE rpg.RoleId = :RoleId

            ORDER BY st.Code, pg.Name";

        var items = (await _connection.QueryAsync<PermissionGroup>(sql, new { RoleId = roleId })).ToList();

        await HydrateOrganizationUnitsAsync(items);

        return items;

    }



    public async Task<IEnumerable<long>> GetOrganizationUnitIdsByGroupIdAsync(long permissionGroupId)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"

            SELECT OrganizationUnitId

            FROM PERMISSION_GROUP_UNIT

            WHERE PermissionGroupId = :PermissionGroupId

            ORDER BY OrganizationUnitId";

        return await _connection.QueryAsync<long>(sql, new { PermissionGroupId = permissionGroupId });

    }



    public async Task AssignOrganizationUnitsAsync(long permissionGroupId, IEnumerable<long> organizationUnitIds)

    {

        if (_connection.State != ConnectionState.Open) _connection.Open();

        using var transaction = _connection.BeginTransaction();

        try

        {

            await ReplaceOrganizationUnitsAsync(permissionGroupId, organizationUnitIds, transaction);

            transaction.Commit();

        }

        catch

        {

            transaction.Rollback();

            throw;

        }

    }



    private async Task HydrateOrganizationUnitsAsync(IEnumerable<PermissionGroup> groups)

    {

        var list = groups.ToList();

        if (list.Count == 0) return;



        var idList = list.Select(g => g.Id).Distinct().ToList();

        var inClause = string.Join(",", idList);

        var sql = $@"

            SELECT PermissionGroupId, OrganizationUnitId

            FROM PERMISSION_GROUP_UNIT

            WHERE PermissionGroupId IN ({inClause})

            ORDER BY PermissionGroupId, OrganizationUnitId";



        var rows = await _connection.QueryAsync<(long PermissionGroupId, long OrganizationUnitId)>(sql);

        var lookup = rows

            .GroupBy(r => r.PermissionGroupId)

            .ToDictionary(g => g.Key, g => g.Select(x => x.OrganizationUnitId).ToList());



        foreach (var group in list)

        {

            if (lookup.TryGetValue(group.Id, out var unitIds) && unitIds.Count > 0)

            {

                group.OrganizationUnitIds = unitIds;

                if (!group.OrganizationUnitId.HasValue)

                {

                    group.OrganizationUnitId = unitIds[0];

                }

            }

            else if (group.OrganizationUnitId.HasValue)

            {

                group.OrganizationUnitIds = new List<long> { group.OrganizationUnitId.Value };

            }

            else

            {

                group.OrganizationUnitIds = new List<long>();

            }



            if (string.IsNullOrWhiteSpace(group.OrganizationUnitNames) && !string.IsNullOrWhiteSpace(group.OrganizationUnitName))

            {

                group.OrganizationUnitNames = group.OrganizationUnitName;

            }

        }

    }



    private async Task ReplaceOrganizationUnitsAsync(long permissionGroupId, IEnumerable<long> organizationUnitIds, IDbTransaction transaction)

    {

        await _connection.ExecuteAsync(

            "DELETE FROM PERMISSION_GROUP_UNIT WHERE PermissionGroupId = :PermissionGroupId",

            new { PermissionGroupId = permissionGroupId },

            transaction);



        const string insertSql = @"

            INSERT INTO PERMISSION_GROUP_UNIT (PermissionGroupId, OrganizationUnitId)

            VALUES (:PermissionGroupId, :OrganizationUnitId)";



        foreach (var unitId in organizationUnitIds.Distinct())

        {

            await _connection.ExecuteAsync(insertSql, new

            {

                PermissionGroupId = permissionGroupId,

                OrganizationUnitId = unitId

            }, transaction);

        }

    }



    private static List<long> NormalizeUnitIds(PermissionGroup group)

    {

        var ids = (group.OrganizationUnitIds ?? new List<long>())

            .Where(id => id > 0)

            .Distinct()

            .ToList();



        if (ids.Count == 0 && group.OrganizationUnitId.HasValue && group.OrganizationUnitId.Value > 0)

        {

            ids.Add(group.OrganizationUnitId.Value);

        }



        return ids;

    }



    private static long ResolveScopeTypeId(PermissionGroup group)

    {

        if (group.ScopeTypeId > 0) return group.ScopeTypeId;

        var code = string.Equals(group.GroupType, "SYSTEM", StringComparison.OrdinalIgnoreCase) ? "GLOBAL" : group.GroupType;

        return string.Equals(code, "UNIT", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    }



    private static string BuildSelectSql(string whereClause) => $@"

        SELECT pg.Id, pg.Code, pg.Name, pg.Description, pg.ScopeTypeId,

               CASE WHEN st.Code = 'GLOBAL' THEN 'SYSTEM' ELSE st.Code END AS GroupType,

               st.Name AS ScopeTypeName, pg.OrganizationUnitId,

               o.Name AS OrganizationUnitName,

               (SELECT LISTAGG(ou.Name, ', ') WITHIN GROUP (ORDER BY ou.Name)

                  FROM PERMISSION_GROUP_UNIT pgu2

                  INNER JOIN ORGANIZATION_UNIT ou ON ou.Id = pgu2.OrganizationUnitId

                 WHERE pgu2.PermissionGroupId = pg.Id) AS OrganizationUnitNames,

               pg.CreatedAt,

               pg.CreatedBy,

               creator.FullName AS CreatedByName,

               pg.IsActive

        FROM PERMISSION_GROUP pg

        INNER JOIN SCOPE_TYPE st ON pg.ScopeTypeId = st.Id

        LEFT JOIN ORGANIZATION_UNIT o ON pg.OrganizationUnitId = o.Id

        LEFT JOIN APP_USER creator ON creator.Id = pg.CreatedBy

        {whereClause}";

}

