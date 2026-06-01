// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Infrastructure\Repositories\UploadConfigRepository.cs
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

public class UploadConfigRepository : IUploadConfigRepository
{
    private readonly string _connectionString;

    public UploadConfigRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<UploadConfig>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, ModuleCode, AllowedExtensions, MaxSizeMb, Description FROM UPLOAD_CONFIG ORDER BY Id";
        return await connection.QueryAsync<UploadConfig>(sql);
    }

    public async Task<UploadConfig?> GetByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, ModuleCode, AllowedExtensions, MaxSizeMb, Description FROM UPLOAD_CONFIG WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<UploadConfig>(sql, new { Id = id });
    }

    public async Task<UploadConfig?> GetByModuleCodeAsync(string moduleCode)
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, ModuleCode, AllowedExtensions, MaxSizeMb, Description FROM UPLOAD_CONFIG WHERE ModuleCode = :ModuleCode";
        return await connection.QuerySingleOrDefaultAsync<UploadConfig>(sql, new { ModuleCode = moduleCode });
    }

    public async Task<long> CreateAsync(UploadConfig config)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO UPLOAD_CONFIG (ModuleCode, AllowedExtensions, MaxSizeMb, Description)
            VALUES (:ModuleCode, :AllowedExtensions, :MaxSizeMb, :Description)
            RETURNING Id INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("ModuleCode", config.ModuleCode);
        parameters.Add("AllowedExtensions", config.AllowedExtensions);
        parameters.Add("MaxSizeMb", config.MaxSizeMb);
        parameters.Add("Description", config.Description);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(UploadConfig config)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE UPLOAD_CONFIG 
            SET ModuleCode = :ModuleCode, 
                AllowedExtensions = :AllowedExtensions, 
                MaxSizeMb = :MaxSizeMb, 
                Description = :Description 
            WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new 
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
        using var connection = CreateConnection();
        var sql = "DELETE FROM UPLOAD_CONFIG WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
