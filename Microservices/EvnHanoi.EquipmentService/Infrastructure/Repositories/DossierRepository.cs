using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DossierRepository : IDossierRepository
{
    private readonly IDbConnection _connection;

    public DossierRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<Dossier?> GetByIdAsync(Guid id)
    {
        var sql = $"SELECT * FROM {nameof(Dossier)}s WHERE {nameof(Dossier.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<Dossier>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Dossier>> GetAllAsync()
    {
        var sql = $"SELECT * FROM {nameof(Dossier)}s";
        return await _connection.QueryAsync<Dossier>(sql);
    }

    public async Task<bool> CreateAsync(Dossier dossier)
    {
        dossier.Version = 1;
        var sql = $@"INSERT INTO {nameof(Dossier)}s (
                        {nameof(Dossier.Id)}, 
                        {nameof(Dossier.EquipmentId)}, 
                        {nameof(Dossier.Title)}, 
                        {nameof(Dossier.Description)}, 
                        {nameof(Dossier.Status)}, 
                        {nameof(Dossier.CreatedAt)}, 
                        {nameof(Dossier.CreatedBy)}, 
                        {nameof(Dossier.Version)}
                    )
                    VALUES (:Id, :EquipmentId, :Title, :Description, :Status, :CreatedAt, :CreatedBy, :Version)";
        var result = await _connection.ExecuteAsync(sql, dossier);
        return result > 0;
    }

    public async Task<bool> UpdateAsync(Dossier dossier)
    {
        var sql = $@"UPDATE {nameof(Dossier)}s 
                    SET {nameof(Dossier.EquipmentId)} = :EquipmentId,
                        {nameof(Dossier.Title)} = :Title, 
                        {nameof(Dossier.Description)} = :Description, 
                        {nameof(Dossier.Status)} = :Status, 
                        {nameof(Dossier.UpdatedAt)} = :UpdatedAt, 
                        {nameof(Dossier.UpdatedBy)} = :UpdatedBy, 
                        {nameof(Dossier.Version)} = {nameof(Dossier.Version)} + 1 
                    WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.Version)} = :Version";

        var affectedRows = await _connection.ExecuteAsync(sql, dossier);
        
        if (affectedRows == 0)
        {
            // If the record exists but version doesn't match, it's a concurrency issue.
            var exists = await _connection.ExecuteScalarAsync<bool>(
                $"SELECT 1 FROM {nameof(Dossier)}s WHERE {nameof(Dossier.Id)} = :Id", new { Id = dossier.Id });
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
        var sql = $"DELETE FROM {nameof(Dossier)}s WHERE {nameof(Dossier.Id)} = :Id";
        var result = await _connection.ExecuteAsync(sql, new { Id = id });
        return result > 0;
    }

    public async Task<bool> CreateVersionAsync(DossierVersion version)
    {
        var sql = $@"INSERT INTO {nameof(DossierVersion)}s (
                        {nameof(DossierVersion.Id)}, 
                        {nameof(DossierVersion.DossierId)}, 
                        {nameof(DossierVersion.VersionNumber)}, 
                        {nameof(DossierVersion.Title)}, 
                        {nameof(DossierVersion.Description)}, 
                        {nameof(DossierVersion.Status)}, 
                        {nameof(DossierVersion.ChangeLog)}, 
                        {nameof(DossierVersion.CreatedAt)}, 
                        {nameof(DossierVersion.CreatedBy)}
                    )
                    VALUES (:Id, :DossierId, :VersionNumber, :Title, :Description, :Status, :ChangeLog, :CreatedAt, :CreatedBy)";
        var result = await _connection.ExecuteAsync(sql, version);
        return result > 0;
    }

    public async Task<IEnumerable<DossierVersion>> GetVersionsAsync(Guid dossierId)
    {
        var sql = $"SELECT * FROM {nameof(DossierVersion)}s WHERE {nameof(DossierVersion.DossierId)} = :DossierId ORDER BY {nameof(DossierVersion.VersionNumber)} DESC";
        return await _connection.QueryAsync<DossierVersion>(sql, new { DossierId = dossierId });
    }
}
