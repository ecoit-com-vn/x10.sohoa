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
                   {nameof(OrganizationUnit.IsActive)}
            FROM ORGANIZATION_UNIT 
            ORDER BY {nameof(OrganizationUnit.Id)}";
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
                   {nameof(OrganizationUnit.IsActive)}
            FROM ORGANIZATION_UNIT 
            WHERE {nameof(OrganizationUnit.Id)} = :Id";
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
                {nameof(OrganizationUnit.IsActive)}
            )
            VALUES (:Code, :Name, :ParentId, :Description, :IsActive)
            RETURNING {nameof(OrganizationUnit.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Code", unit.Code);
        parameters.Add("Name", unit.Name);
        parameters.Add("ParentId", unit.ParentId);
        parameters.Add("Description", unit.Description);
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
                {nameof(OrganizationUnit.IsActive)} = :IsActive,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE {nameof(OrganizationUnit.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new 
        {
            unit.Code,
            unit.Name,
            unit.ParentId,
            unit.Description,
            IsActive = unit.IsActive ? 1 : 0,
            unit.Id
        });
        return affected > 0;
    }

    public async Task<IEnumerable<OrganizationUnit>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        if (startUnitId.HasValue)
        {
            var sql = $@"SELECT {nameof(OrganizationUnit.Id)}, 
                               {nameof(OrganizationUnit.Code)}, 
                               {nameof(OrganizationUnit.Name)}, 
                               {nameof(OrganizationUnit.ParentId)}, 
                               {nameof(OrganizationUnit.Description)},
                               {nameof(OrganizationUnit.IsActive)}
                        FROM ORGANIZATION_UNIT
                        START WITH Id = :StartUnitId
                        CONNECT BY PRIOR Id = ParentId";
            return await _connection.QueryAsync<OrganizationUnit>(sql, new { StartUnitId = startUnitId.Value });
        }
        else
        {
            return await GetAllAsync();
        }
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $"DELETE FROM ORGANIZATION_UNIT WHERE {nameof(OrganizationUnit.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
