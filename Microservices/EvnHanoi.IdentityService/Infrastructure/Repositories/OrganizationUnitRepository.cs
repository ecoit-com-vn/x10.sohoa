using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class OrganizationUnitRepository : IOrganizationUnitRepository
{
    private readonly IDbConnection _connection;
    private const string ActiveOnlyFilter = "IsDeleted = 0";

    public OrganizationUnitRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<OrganizationUnit>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(OrganizationUnit.Id)}, 
                   {nameof(OrganizationUnit.Code)}, 
                   {nameof(OrganizationUnit.Name)}, 
                   {nameof(OrganizationUnit.ParentId)}, 
                   {nameof(OrganizationUnit.Description)},
                   SORTORDER,
                   {nameof(OrganizationUnit.IsActive)},
                   {nameof(OrganizationUnit.IsDeleted)}
            FROM ORGANIZATION_UNIT 
            WHERE {ActiveOnlyFilter}
            ORDER BY CASE WHEN SORTORDER IS NULL THEN 1 ELSE 0 END,
                     SORTORDER ASC NULLS LAST,
                     CASE WHEN ISACTIVE = 1 THEN 0 ELSE 1 END,
                     CREATEDAT DESC NULLS LAST,
                     ID DESC";
        return await _connection.QueryAsync<OrganizationUnit>(sql);
    }

    public async Task<OrganizationUnit?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(OrganizationUnit.Id)}, 
                   {nameof(OrganizationUnit.Code)}, 
                   {nameof(OrganizationUnit.Name)}, 
                   {nameof(OrganizationUnit.ParentId)}, 
                   {nameof(OrganizationUnit.Description)},
                   SORTORDER,
                   {nameof(OrganizationUnit.IsActive)},
                   {nameof(OrganizationUnit.IsDeleted)}
            FROM ORGANIZATION_UNIT 
            WHERE {nameof(OrganizationUnit.Id)} = :Id AND {ActiveOnlyFilter}";
        return await _connection.QuerySingleOrDefaultAsync<OrganizationUnit>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(OrganizationUnit unit)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            INSERT INTO ORGANIZATION_UNIT (
                {nameof(OrganizationUnit.Code)}, 
                {nameof(OrganizationUnit.Name)}, 
                {nameof(OrganizationUnit.ParentId)}, 
                {nameof(OrganizationUnit.Description)},
                SORTORDER,
                {nameof(OrganizationUnit.IsActive)},
                {nameof(OrganizationUnit.IsDeleted)}
            )
            VALUES (:Code, :Name, :ParentId, :Description, :SortOrder, :IsActive, 0)
            RETURNING {nameof(OrganizationUnit.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Code", unit.Code);
        parameters.Add("Name", unit.Name);
        parameters.Add("ParentId", unit.ParentId);
        parameters.Add("Description", unit.Description);
        parameters.Add("SortOrder", unit.SortOrder);
        parameters.Add("IsActive", unit.IsActive ? 1 : 0);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(OrganizationUnit unit)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE ORGANIZATION_UNIT 
            SET {nameof(OrganizationUnit.Code)} = :Code, 
                {nameof(OrganizationUnit.Name)} = :Name, 
                {nameof(OrganizationUnit.ParentId)} = :ParentId,
                {nameof(OrganizationUnit.Description)} = :Description,
                SORTORDER = :SortOrder,
                {nameof(OrganizationUnit.IsActive)} = :IsActive,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE {nameof(OrganizationUnit.Id)} = :Id AND {ActiveOnlyFilter}";
        var affected = await _connection.ExecuteAsync(sql, new 
        {
            unit.Code,
            unit.Name,
            unit.ParentId,
            unit.Description,
            unit.SortOrder,
            IsActive = unit.IsActive ? 1 : 0,
            unit.Id
        });
        return affected > 0;
    }

    public async Task<IEnumerable<OrganizationUnit>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var startCondition = startUnitId.HasValue
            ? $"{nameof(OrganizationUnit.Id)} = :StartUnitId"
            : $"{nameof(OrganizationUnit.ParentId)} IS NULL";
        var sql = $@"SELECT {nameof(OrganizationUnit.Id)},
                           {nameof(OrganizationUnit.Code)},
                           {nameof(OrganizationUnit.Name)},
                           {nameof(OrganizationUnit.ParentId)},
                           {nameof(OrganizationUnit.Description)},
                           SORTORDER,
                           {nameof(OrganizationUnit.IsActive)},
                           {nameof(OrganizationUnit.IsDeleted)}
                    FROM ORGANIZATION_UNIT
                    WHERE {ActiveOnlyFilter}
                    START WITH {startCondition}
                    CONNECT BY NOCYCLE PRIOR Id = ParentId
                    ORDER SIBLINGS BY SORTORDER ASC NULLS LAST,
                                      {nameof(OrganizationUnit.Code)} ASC,
                                      {nameof(OrganizationUnit.Id)} ASC";
        return await _connection.QueryAsync<OrganizationUnit>(
            sql,
            startUnitId.HasValue ? new { StartUnitId = startUnitId.Value } : null);
    }

    public async Task<bool> HasActiveChildrenAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"SELECT COUNT(*) FROM ORGANIZATION_UNIT 
                     WHERE {nameof(OrganizationUnit.ParentId)} = :Id AND {ActiveOnlyFilter}";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return count > 0;
    }

    public async Task<bool> HasActiveUsersAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = "SELECT COUNT(*) FROM APP_USER WHERE OrganizationUnitId = :Id AND IsDeleted = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return count > 0;
    }

    public async Task<bool> HasActiveFoldersAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = "SELECT COUNT(*) FROM FOLDERS WHERE UNIT_ID = :Id AND IS_DELETED = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return count > 0;
    }

    public async Task<bool> HasActiveInfrastructureAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        const string sql = "SELECT COUNT(*) FROM INFRASTRUCTURE WHERE UNIT_ID = :Id AND IsDeleted = 0";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return count > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE ORGANIZATION_UNIT 
            SET {nameof(OrganizationUnit.IsDeleted)} = 1,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE {nameof(OrganizationUnit.Id)} = :Id AND {ActiveOnlyFilter}";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
