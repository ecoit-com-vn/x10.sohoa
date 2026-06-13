using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UploadConfigRepository : IUploadConfigRepository
{
    private readonly IDbConnection _connection;

    public UploadConfigRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<UploadConfig>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT u.{nameof(UploadConfig.Id)}, 
                   u.{nameof(UploadConfig.Name)}, 
                   u.{nameof(UploadConfig.AllowedExtensions)}, 
                   u.{nameof(UploadConfig.MaxSizeMb)}, 
                   u.{nameof(UploadConfig.Description)}, 
                   u.{nameof(UploadConfig.OrganizationUnitId)}, 
                   u.{nameof(UploadConfig.IsActive)}, 
                   o.{nameof(OrganizationUnit.Name)} AS {nameof(UploadConfig.OrganizationUnitName)}
            FROM UPLOAD_CONFIG u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(UploadConfig.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)}
            ORDER BY u.{nameof(UploadConfig.Id)}";
        return await _connection.QueryAsync<UploadConfig>(sql);
    }

    public async Task<UploadConfig?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT u.{nameof(UploadConfig.Id)}, 
                   u.{nameof(UploadConfig.Name)}, 
                   u.{nameof(UploadConfig.AllowedExtensions)}, 
                   u.{nameof(UploadConfig.MaxSizeMb)}, 
                   u.{nameof(UploadConfig.Description)}, 
                   u.{nameof(UploadConfig.OrganizationUnitId)}, 
                   u.{nameof(UploadConfig.IsActive)}, 
                   o.{nameof(OrganizationUnit.Name)} AS {nameof(UploadConfig.OrganizationUnitName)}
            FROM UPLOAD_CONFIG u
            LEFT JOIN ORGANIZATION_UNIT o ON u.{nameof(UploadConfig.OrganizationUnitId)} = o.{nameof(OrganizationUnit.Id)}
            WHERE u.{nameof(UploadConfig.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<UploadConfig>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(UploadConfig config)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            INSERT INTO UPLOAD_CONFIG (
                {nameof(UploadConfig.Name)}, 
                {nameof(UploadConfig.AllowedExtensions)}, 
                {nameof(UploadConfig.MaxSizeMb)}, 
                {nameof(UploadConfig.Description)},
                {nameof(UploadConfig.OrganizationUnitId)},
                {nameof(UploadConfig.IsActive)}
            )
            VALUES (:Name, :AllowedExtensions, :MaxSizeMb, :Description, :OrganizationUnitId, :IsActive)
            RETURNING {nameof(UploadConfig.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Name", config.Name);
        parameters.Add("AllowedExtensions", config.AllowedExtensions);
        parameters.Add("MaxSizeMb", config.MaxSizeMb);
        parameters.Add("Description", config.Description);
        parameters.Add("OrganizationUnitId", config.OrganizationUnitId);
        parameters.Add("IsActive", config.IsActive ? 1 : 0);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(UploadConfig config)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE UPLOAD_CONFIG 
            SET {nameof(UploadConfig.Name)} = :Name, 
                {nameof(UploadConfig.AllowedExtensions)} = :AllowedExtensions, 
                {nameof(UploadConfig.MaxSizeMb)} = :MaxSizeMb, 
                {nameof(UploadConfig.Description)} = :Description,
                {nameof(UploadConfig.OrganizationUnitId)} = :OrganizationUnitId,
                {nameof(UploadConfig.IsActive)} = :IsActive
            WHERE {nameof(UploadConfig.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new 
        {
            config.Name,
            config.AllowedExtensions,
            config.MaxSizeMb,
            config.Description,
            config.OrganizationUnitId,
            IsActive = config.IsActive ? 1 : 0,
            config.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $"DELETE FROM UPLOAD_CONFIG WHERE {nameof(UploadConfig.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
