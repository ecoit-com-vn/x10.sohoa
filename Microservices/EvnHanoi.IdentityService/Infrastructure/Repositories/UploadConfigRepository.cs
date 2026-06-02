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
            SELECT {nameof(UploadConfig.Id)}, 
                   {nameof(UploadConfig.ModuleCode)}, 
                   {nameof(UploadConfig.AllowedExtensions)}, 
                   {nameof(UploadConfig.MaxSizeMb)}, 
                   {nameof(UploadConfig.Description)} 
            FROM UPLOAD_CONFIG 
            ORDER BY {nameof(UploadConfig.Id)}";
        return await _connection.QueryAsync<UploadConfig>(sql);
    }

    public async Task<UploadConfig?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UploadConfig.Id)}, 
                   {nameof(UploadConfig.ModuleCode)}, 
                   {nameof(UploadConfig.AllowedExtensions)}, 
                   {nameof(UploadConfig.MaxSizeMb)}, 
                   {nameof(UploadConfig.Description)} 
            FROM UPLOAD_CONFIG 
            WHERE {nameof(UploadConfig.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<UploadConfig>(sql, new { Id = id });
    }

    public async Task<UploadConfig?> GetByModuleCodeAsync(string moduleCode)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UploadConfig.Id)}, 
                   {nameof(UploadConfig.ModuleCode)}, 
                   {nameof(UploadConfig.AllowedExtensions)}, 
                   {nameof(UploadConfig.MaxSizeMb)}, 
                   {nameof(UploadConfig.Description)} 
            FROM UPLOAD_CONFIG 
            WHERE {nameof(UploadConfig.ModuleCode)} = :ModuleCode";
        return await _connection.QuerySingleOrDefaultAsync<UploadConfig>(sql, new { ModuleCode = moduleCode });
    }

    public async Task<long> CreateAsync(UploadConfig config)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            INSERT INTO UPLOAD_CONFIG (
                {nameof(UploadConfig.ModuleCode)}, 
                {nameof(UploadConfig.AllowedExtensions)}, 
                {nameof(UploadConfig.MaxSizeMb)}, 
                {nameof(UploadConfig.Description)}
            )
            VALUES (:ModuleCode, :AllowedExtensions, :MaxSizeMb, :Description)
            RETURNING {nameof(UploadConfig.Id)} INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("ModuleCode", config.ModuleCode);
        parameters.Add("AllowedExtensions", config.AllowedExtensions);
        parameters.Add("MaxSizeMb", config.MaxSizeMb);
        parameters.Add("Description", config.Description);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(UploadConfig config)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE UPLOAD_CONFIG 
            SET {nameof(UploadConfig.ModuleCode)} = :ModuleCode, 
                {nameof(UploadConfig.AllowedExtensions)} = :AllowedExtensions, 
                {nameof(UploadConfig.MaxSizeMb)} = :MaxSizeMb, 
                {nameof(UploadConfig.Description)} = :Description 
            WHERE {nameof(UploadConfig.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new 
        {
            config.ModuleCode,
            config.AllowedExtensions,
            config.MaxSizeMb,
            config.Description,
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
