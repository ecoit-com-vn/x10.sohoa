using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class PhysicalStorageRepository : IPhysicalStorageRepository
{
    private readonly IDbConnection _connection;

    public PhysicalStorageRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    // Fonds
    public async Task<IEnumerable<PhysicalFonds>> GetAllFondsAsync()
    {
        return await _connection.QueryAsync<PhysicalFonds>(
            $"SELECT * FROM PHYSICAL_FONDS ORDER BY {nameof(PhysicalFonds.Id)}");
    }

    public async Task<PhysicalFonds?> GetFondsByIdAsync(long id)
    {
        return await _connection.QuerySingleOrDefaultAsync<PhysicalFonds>(
            $"SELECT * FROM PHYSICAL_FONDS WHERE {nameof(PhysicalFonds.Id)} = :Id", new { Id = id });
    }

    public async Task<long> CreateFondsAsync(PhysicalFonds fonds)
    {
        var sql = $@"INSERT INTO PHYSICAL_FONDS (
                        {nameof(PhysicalFonds.Code)}, 
                        {nameof(PhysicalFonds.Name)}, 
                        {nameof(PhysicalFonds.Description)}, 
                        {nameof(PhysicalFonds.CreatedBy)}
                    ) 
                    VALUES (:Code, :Name, :Description, :CreatedBy) 
                    RETURNING {nameof(PhysicalFonds.Id)} INTO :Id";
        var parameters = new DynamicParameters(fonds);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateFondsAsync(PhysicalFonds fonds)
    {
        var sql = $@"UPDATE PHYSICAL_FONDS SET 
                        {nameof(PhysicalFonds.Code)} = :Code, 
                        {nameof(PhysicalFonds.Name)} = :Name, 
                        {nameof(PhysicalFonds.Description)} = :Description, 
                        {nameof(PhysicalFonds.UpdatedAt)} = CURRENT_TIMESTAMP, 
                        {nameof(PhysicalFonds.UpdatedBy)} = :UpdatedBy 
                    WHERE {nameof(PhysicalFonds.Id)} = :Id";
        return await _connection.ExecuteAsync(sql, fonds) > 0;
    }

    public async Task<bool> DeleteFondsAsync(long id)
    {
        return await _connection.ExecuteAsync(
            $"DELETE FROM PHYSICAL_FONDS WHERE {nameof(PhysicalFonds.Id)} = :Id", new { Id = id }) > 0;
    }

    // Shelf
    public async Task<IEnumerable<PhysicalShelf>> GetShelvesByFondsIdAsync(long fondsId)
    {
        return await _connection.QueryAsync<PhysicalShelf>(
            $"SELECT * FROM PHYSICAL_SHELF WHERE {nameof(PhysicalShelf.FondsId)} = :FondsId ORDER BY {nameof(PhysicalShelf.Id)}", new { FondsId = fondsId });
    }

    public async Task<PhysicalShelf?> GetShelfByIdAsync(long id)
    {
        return await _connection.QuerySingleOrDefaultAsync<PhysicalShelf>(
            $"SELECT * FROM PHYSICAL_SHELF WHERE {nameof(PhysicalShelf.Id)} = :Id", new { Id = id });
    }

    public async Task<long> CreateShelfAsync(PhysicalShelf shelf)
    {
        var sql = $@"INSERT INTO PHYSICAL_SHELF (
                        {nameof(PhysicalShelf.FondsId)}, 
                        {nameof(PhysicalShelf.Code)}, 
                        {nameof(PhysicalShelf.Name)}, 
                        {nameof(PhysicalShelf.Description)}, 
                        {nameof(PhysicalShelf.CreatedBy)}
                    ) 
                    VALUES (:FondsId, :Code, :Name, :Description, :CreatedBy) 
                    RETURNING {nameof(PhysicalShelf.Id)} INTO :Id";
        var parameters = new DynamicParameters(shelf);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateShelfAsync(PhysicalShelf shelf)
    {
        var sql = $@"UPDATE PHYSICAL_SHELF SET 
                        {nameof(PhysicalShelf.FondsId)} = :FondsId, 
                        {nameof(PhysicalShelf.Code)} = :Code, 
                        {nameof(PhysicalShelf.Name)} = :Name, 
                        {nameof(PhysicalShelf.Description)} = :Description, 
                        {nameof(PhysicalShelf.UpdatedAt)} = CURRENT_TIMESTAMP, 
                        {nameof(PhysicalShelf.UpdatedBy)} = :UpdatedBy 
                    WHERE {nameof(PhysicalShelf.Id)} = :Id";
        return await _connection.ExecuteAsync(sql, shelf) > 0;
    }

    public async Task<bool> DeleteShelfAsync(long id)
    {
        return await _connection.ExecuteAsync(
            $"DELETE FROM PHYSICAL_SHELF WHERE {nameof(PhysicalShelf.Id)} = :Id", new { Id = id }) > 0;
    }

    // Floor
    public async Task<IEnumerable<PhysicalFloor>> GetFloorsByShelfIdAsync(long shelfId)
    {
        return await _connection.QueryAsync<PhysicalFloor>(
            $"SELECT * FROM PHYSICAL_FLOOR WHERE {nameof(PhysicalFloor.ShelfId)} = :ShelfId ORDER BY {nameof(PhysicalFloor.Id)}", new { ShelfId = shelfId });
    }

    public async Task<PhysicalFloor?> GetFloorByIdAsync(long id)
    {
        return await _connection.QuerySingleOrDefaultAsync<PhysicalFloor>(
            $"SELECT * FROM PHYSICAL_FLOOR WHERE {nameof(PhysicalFloor.Id)} = :Id", new { Id = id });
    }

    public async Task<long> CreateFloorAsync(PhysicalFloor floor)
    {
        var sql = $@"INSERT INTO PHYSICAL_FLOOR (
                        {nameof(PhysicalFloor.ShelfId)}, 
                        {nameof(PhysicalFloor.Code)}, 
                        {nameof(PhysicalFloor.Name)}, 
                        {nameof(PhysicalFloor.Description)}, 
                        {nameof(PhysicalFloor.CreatedBy)}
                    ) 
                    VALUES (:ShelfId, :Code, :Name, :Description, :CreatedBy) 
                    RETURNING {nameof(PhysicalFloor.Id)} INTO :Id";
        var parameters = new DynamicParameters(floor);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateFloorAsync(PhysicalFloor floor)
    {
        var sql = $@"UPDATE PHYSICAL_FLOOR SET 
                        {nameof(PhysicalFloor.ShelfId)} = :ShelfId, 
                        {nameof(PhysicalFloor.Code)} = :Code, 
                        {nameof(PhysicalFloor.Name)} = :Name, 
                        {nameof(PhysicalFloor.Description)} = :Description, 
                        {nameof(PhysicalFloor.UpdatedAt)} = CURRENT_TIMESTAMP, 
                        {nameof(PhysicalFloor.UpdatedBy)} = :UpdatedBy 
                    WHERE {nameof(PhysicalFloor.Id)} = :Id";
        return await _connection.ExecuteAsync(sql, floor) > 0;
    }

    public async Task<bool> DeleteFloorAsync(long id)
    {
        return await _connection.ExecuteAsync(
            $"DELETE FROM PHYSICAL_FLOOR WHERE {nameof(PhysicalFloor.Id)} = :Id", new { Id = id }) > 0;
    }

    // Box
    public async Task<IEnumerable<PhysicalBox>> GetBoxesByFloorIdAsync(long floorId)
    {
        return await _connection.QueryAsync<PhysicalBox>(
            $"SELECT * FROM PHYSICAL_BOX WHERE {nameof(PhysicalBox.FloorId)} = :FloorId ORDER BY {nameof(PhysicalBox.Id)}", new { FloorId = floorId });
    }

    public async Task<PhysicalBox?> GetBoxByIdAsync(long id)
    {
        return await _connection.QuerySingleOrDefaultAsync<PhysicalBox>(
            $"SELECT * FROM PHYSICAL_BOX WHERE {nameof(PhysicalBox.Id)} = :Id", new { Id = id });
    }

    public async Task<long> CreateBoxAsync(PhysicalBox box)
    {
        var sql = $@"INSERT INTO PHYSICAL_BOX (
                        {nameof(PhysicalBox.FloorId)}, 
                        {nameof(PhysicalBox.Code)}, 
                        {nameof(PhysicalBox.Name)}, 
                        {nameof(PhysicalBox.Description)}, 
                        {nameof(PhysicalBox.CreatedBy)}
                    ) 
                    VALUES (:FloorId, :Code, :Name, :Description, :CreatedBy) 
                    RETURNING {nameof(PhysicalBox.Id)} INTO :Id";
        var parameters = new DynamicParameters(box);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateBoxAsync(PhysicalBox box)
    {
        var sql = $@"UPDATE PHYSICAL_BOX SET 
                        {nameof(PhysicalBox.FloorId)} = :FloorId, 
                        {nameof(PhysicalBox.Code)} = :Code, 
                        {nameof(PhysicalBox.Name)} = :Name, 
                        {nameof(PhysicalBox.Description)} = :Description, 
                        {nameof(PhysicalBox.UpdatedAt)} = CURRENT_TIMESTAMP, 
                        {nameof(PhysicalBox.UpdatedBy)} = :UpdatedBy 
                    WHERE {nameof(PhysicalBox.Id)} = :Id";
        return await _connection.ExecuteAsync(sql, box) > 0;
    }

    public async Task<bool> DeleteBoxAsync(long id)
    {
        return await _connection.ExecuteAsync(
            $"DELETE FROM PHYSICAL_BOX WHERE {nameof(PhysicalBox.Id)} = :Id", new { Id = id }) > 0;
    }
}
