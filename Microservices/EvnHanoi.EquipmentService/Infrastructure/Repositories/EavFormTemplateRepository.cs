using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EavFormTemplateRepository : IEavFormTemplateRepository
{
    private readonly string _connectionString;

    public EavFormTemplateRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<EavFormTemplate?> GetByIdAsync(Guid id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM EavFormTemplates WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { Id = id });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM EavFormTemplates WHERE IsActive = 1";
        return await connection.QueryAsync<EavFormTemplate>(sql);
    }

    public async Task AddAsync(EavFormTemplate template)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO EavFormTemplates (Id, Name, Description, Schema, Version, IsActive, CreatedAt, CreatedBy)
                    VALUES (:Id, :Name, :Description, :Schema, :Version, :IsActive, :CreatedAt, :CreatedBy)";
        
        var param = new
        {
            template.Id,
            template.Name,
            template.Description,
            template.Schema,
            template.Version,
            IsActive = template.IsActive ? 1 : 0,
            template.CreatedAt,
            template.CreatedBy
        };

        await connection.ExecuteAsync(sql, param);
    }

    public async Task UpdateAsync(EavFormTemplate template)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE EavFormTemplates
                    SET Name = :Name,
                        Description = :Description,
                        Schema = :Schema,
                        Version = :Version,
                        IsActive = :IsActive
                    WHERE Id = :Id";
        
        var param = new
        {
            template.Id,
            template.Name,
            template.Description,
            template.Schema,
            template.Version,
            IsActive = template.IsActive ? 1 : 0
        };

        await connection.ExecuteAsync(sql, param);
    }
}
