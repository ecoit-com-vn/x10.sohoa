using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class PhysicalStorageRepository : IPhysicalStorageRepository
{
    private readonly string _connectionString;

    public PhysicalStorageRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    // Fonds
    public async Task<IEnumerable<PhysicalFonds>> GetAllFondsAsync()
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<PhysicalFonds>("SELECT * FROM PHYSICAL_FONDS ORDER BY Id");
    }

    public async Task<PhysicalFonds?> GetFondsByIdAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PhysicalFonds>(
            "SELECT * FROM PHYSICAL_FONDS WHERE Id = :Id", new { Id = id });
    }

    public async Task<long> CreateFondsAsync(PhysicalFonds fonds)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO PHYSICAL_FONDS (Code, Name, Description, CreatedBy) 
                    VALUES (:Code, :Name, :Description, :CreatedBy) RETURNING Id INTO :Id";
        var parameters = new DynamicParameters(fonds);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateFondsAsync(PhysicalFonds fonds)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE PHYSICAL_FONDS SET Code = :Code, Name = :Name, Description = :Description, 
                    UpdatedAt = CURRENT_TIMESTAMP, UpdatedBy = :UpdatedBy WHERE Id = :Id";
        return await connection.ExecuteAsync(sql, fonds) > 0;
    }

    public async Task<bool> DeleteFondsAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.ExecuteAsync("DELETE FROM PHYSICAL_FONDS WHERE Id = :Id", new { Id = id }) > 0;
    }

    // Shelf
    public async Task<IEnumerable<PhysicalShelf>> GetShelvesByFondsIdAsync(long fondsId)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<PhysicalShelf>(
            "SELECT * FROM PHYSICAL_SHELF WHERE FondsId = :FondsId ORDER BY Id", new { FondsId = fondsId });
    }

    public async Task<PhysicalShelf?> GetShelfByIdAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PhysicalShelf>(
            "SELECT * FROM PHYSICAL_SHELF WHERE Id = :Id", new { Id = id });
    }

    public async Task<long> CreateShelfAsync(PhysicalShelf shelf)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO PHYSICAL_SHELF (FondsId, Code, Name, Description, CreatedBy) 
                    VALUES (:FondsId, :Code, :Name, :Description, :CreatedBy) RETURNING Id INTO :Id";
        var parameters = new DynamicParameters(shelf);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateShelfAsync(PhysicalShelf shelf)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE PHYSICAL_SHELF SET FondsId = :FondsId, Code = :Code, Name = :Name, Description = :Description, 
                    UpdatedAt = CURRENT_TIMESTAMP, UpdatedBy = :UpdatedBy WHERE Id = :Id";
        return await connection.ExecuteAsync(sql, shelf) > 0;
    }

    public async Task<bool> DeleteShelfAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.ExecuteAsync("DELETE FROM PHYSICAL_SHELF WHERE Id = :Id", new { Id = id }) > 0;
    }

    // Floor
    public async Task<IEnumerable<PhysicalFloor>> GetFloorsByShelfIdAsync(long shelfId)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<PhysicalFloor>(
            "SELECT * FROM PHYSICAL_FLOOR WHERE ShelfId = :ShelfId ORDER BY Id", new { ShelfId = shelfId });
    }

    public async Task<PhysicalFloor?> GetFloorByIdAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PhysicalFloor>(
            "SELECT * FROM PHYSICAL_FLOOR WHERE Id = :Id", new { Id = id });
    }

    public async Task<long> CreateFloorAsync(PhysicalFloor floor)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO PHYSICAL_FLOOR (ShelfId, Code, Name, Description, CreatedBy) 
                    VALUES (:ShelfId, :Code, :Name, :Description, :CreatedBy) RETURNING Id INTO :Id";
        var parameters = new DynamicParameters(floor);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateFloorAsync(PhysicalFloor floor)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE PHYSICAL_FLOOR SET ShelfId = :ShelfId, Code = :Code, Name = :Name, Description = :Description, 
                    UpdatedAt = CURRENT_TIMESTAMP, UpdatedBy = :UpdatedBy WHERE Id = :Id";
        return await connection.ExecuteAsync(sql, floor) > 0;
    }

    public async Task<bool> DeleteFloorAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.ExecuteAsync("DELETE FROM PHYSICAL_FLOOR WHERE Id = :Id", new { Id = id }) > 0;
    }

    // Box
    public async Task<IEnumerable<PhysicalBox>> GetBoxesByFloorIdAsync(long floorId)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<PhysicalBox>(
            "SELECT * FROM PHYSICAL_BOX WHERE FloorId = :FloorId ORDER BY Id", new { FloorId = floorId });
    }

    public async Task<PhysicalBox?> GetBoxByIdAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PhysicalBox>(
            "SELECT * FROM PHYSICAL_BOX WHERE Id = :Id", new { Id = id });
    }

    public async Task<long> CreateBoxAsync(PhysicalBox box)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO PHYSICAL_BOX (FloorId, Code, Name, Description, CreatedBy) 
                    VALUES (:FloorId, :Code, :Name, :Description, :CreatedBy) RETURNING Id INTO :Id";
        var parameters = new DynamicParameters(box);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateBoxAsync(PhysicalBox box)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE PHYSICAL_BOX SET FloorId = :FloorId, Code = :Code, Name = :Name, Description = :Description, 
                    UpdatedAt = CURRENT_TIMESTAMP, UpdatedBy = :UpdatedBy WHERE Id = :Id";
        return await connection.ExecuteAsync(sql, box) > 0;
    }

    public async Task<bool> DeleteBoxAsync(long id)
    {
        using var connection = CreateConnection();
        return await connection.ExecuteAsync("DELETE FROM PHYSICAL_BOX WHERE Id = :Id", new { Id = id }) > 0;
    }
}
