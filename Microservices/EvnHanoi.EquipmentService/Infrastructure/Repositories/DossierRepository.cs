using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DossierRepository : IDossierRepository
{
    private readonly string _connectionString;

    public DossierRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<Dossier?> GetByIdAsync(Guid id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM Dossiers WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<Dossier>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Dossier>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM Dossiers";
        return await connection.QueryAsync<Dossier>(sql);
    }

    public async Task<bool> CreateAsync(Dossier dossier)
    {
        using var connection = CreateConnection();
        dossier.Version = 1;
        var sql = @"INSERT INTO Dossiers (Id, EquipmentId, Title, Description, Status, CreatedAt, CreatedBy, Version)
                    VALUES (:Id, :EquipmentId, :Title, :Description, :Status, :CreatedAt, :CreatedBy, :Version)";
        var result = await connection.ExecuteAsync(sql, dossier);
        return result > 0;
    }

    public async Task<bool> UpdateAsync(Dossier dossier)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE Dossiers 
                    SET EquipmentId = :EquipmentId,
                        Title = :Title, 
                        Description = :Description, 
                        Status = :Status, 
                        UpdatedAt = :UpdatedAt, 
                        UpdatedBy = :UpdatedBy, 
                        Version = Version + 1 
                    WHERE Id = :Id AND Version = :Version";

        var affectedRows = await connection.ExecuteAsync(sql, dossier);
        
        if (affectedRows == 0)
        {
            // If the record exists but version doesn't match, it's a concurrency issue.
            var exists = await connection.ExecuteScalarAsync<bool>("SELECT 1 FROM Dossiers WHERE Id = :Id", new { Id = dossier.Id });
            if (exists)
            {
                throw new Exception("Concurrency conflict occurred. The dossier was updated by another user.");
            }
            return false;
        }

        // Update the model version after successful update
        dossier.Version++;
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM Dossiers WHERE Id = :Id";
        var result = await connection.ExecuteAsync(sql, new { Id = id });
        return result > 0;
    }

    public async Task<bool> CreateVersionAsync(DossierVersion version)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO DossierVersions (Id, DossierId, VersionNumber, Title, Description, Status, ChangeLog, CreatedAt, CreatedBy)
                    VALUES (:Id, :DossierId, :VersionNumber, :Title, :Description, :Status, :ChangeLog, :CreatedAt, :CreatedBy)";
        var result = await connection.ExecuteAsync(sql, version);
        return result > 0;
    }

    public async Task<IEnumerable<DossierVersion>> GetVersionsAsync(Guid dossierId)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM DossierVersions WHERE DossierId = :DossierId ORDER BY VersionNumber DESC";
        return await connection.QueryAsync<DossierVersion>(sql, new { DossierId = dossierId });
    }
}
