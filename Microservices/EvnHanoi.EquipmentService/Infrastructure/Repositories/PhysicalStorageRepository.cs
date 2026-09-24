using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class PhysicalStorageRepository : IPhysicalStorageRepository
{
    private readonly IDbConnection _connection;

    private const string ShelfSelectColumns = @"
        s.Id,
        s.UnitId,
        u.Name AS UnitName,
        s.Code,
        s.Name,
        s.Description,
        NVL(s.STATUS, 1) AS Status,
        NVL(s.IS_DELETED, 0) AS IsDeleted,
        NVL(s.PRIORITY, 1) AS Priority,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy";

    public PhysicalStorageRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<IEnumerable<PhysicalShelf>> GetShelvesAsync(IEnumerable<long>? unitIds = null)
    {
        var sql = $@"
            SELECT {ShelfSelectColumns}
            FROM PHYSICAL_SHELF s
            LEFT JOIN ORGANIZATION_UNIT u ON s.UnitId = u.Id
            WHERE NVL(s.IS_DELETED, 0) = 0";
        var parameters = new DynamicParameters();
        if (unitIds != null)
        {
            var ids = unitIds.Distinct().ToArray();
            if (ids.Length == 0)
                return Enumerable.Empty<PhysicalShelf>();
            sql += " AND s.UnitId IN :UnitIds";
            parameters.Add("UnitIds", ids);
        }
        sql += " ORDER BY NVL(s.PRIORITY, 1), s.Code, s.Id";
        return await _connection.QueryAsync<PhysicalShelf>(sql, parameters);
    }

    public async Task<PhysicalShelf?> GetShelfByIdAsync(long id)
    {
        var sql = $@"
            SELECT {ShelfSelectColumns}
            FROM PHYSICAL_SHELF s
            LEFT JOIN ORGANIZATION_UNIT u ON s.UnitId = u.Id
            WHERE s.Id = :Id AND NVL(s.IS_DELETED, 0) = 0";
        return await _connection.QuerySingleOrDefaultAsync<PhysicalShelf>(sql, new { Id = id });
    }

    public async Task<long> CreateShelfAsync(PhysicalShelf shelf)
    {
        var sql = @"
            INSERT INTO PHYSICAL_SHELF (
                UnitId,
                Code,
                Name,
                Description,
                Priority,
                CreatedBy
            )
            VALUES (
                :UnitId,
                :Code,
                :Name,
                :Description,
                NVL(:Priority, 1),
                :CreatedBy
            )
            RETURNING Id INTO :Id";
        var parameters = new DynamicParameters(shelf);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateShelfAsync(PhysicalShelf shelf)
    {
        var sql = @"
            UPDATE PHYSICAL_SHELF SET
                UnitId = :UnitId,
                Code = :Code,
                Name = :Name,
                Description = :Description,
                Priority = NVL(:Priority, 1),
                UpdatedAt = CURRENT_TIMESTAMP,
                UpdatedBy = :UpdatedBy
            WHERE Id = :Id AND NVL(IS_DELETED, 0) = 0";
        return await _connection.ExecuteAsync(sql, shelf) > 0;
    }

    public async Task<bool> DeleteShelfAsync(long id)
    {
        // Soft delete theo quy ước dự án
        return await _connection.ExecuteAsync(
            @"UPDATE PHYSICAL_SHELF SET IS_DELETED = 1, UpdatedAt = CURRENT_TIMESTAMP
              WHERE Id = :Id AND NVL(IS_DELETED, 0) = 0",
            new { Id = id }) > 0;
    }

    // Floor
    public async Task<IEnumerable<PhysicalFloor>> GetFloorsByShelfIdAsync(long shelfId)
    {
        return await _connection.QueryAsync<PhysicalFloor>(
            $"SELECT * FROM PHYSICAL_FLOOR WHERE {nameof(PhysicalFloor.ShelfId)} = :ShelfId AND NVL(IS_DELETED, 0) = 0 ORDER BY {nameof(PhysicalFloor.Id)}",
            new { ShelfId = shelfId });
    }

    public async Task<IEnumerable<PhysicalFloor>> GetFloorsByUnitIdsAsync(IEnumerable<long>? unitIds = null)
    {
        var sql = @"
            SELECT f.Id, f.ShelfId, f.Code, f.Name, f.Description,
                   NVL(f.STATUS, 1) AS Status,
                   NVL(f.IS_DELETED, 0) AS IsDeleted,
                   NVL(f.PRIORITY, 1) AS Priority,
                   f.CreatedAt, f.CreatedBy, f.UpdatedAt, f.UpdatedBy
            FROM PHYSICAL_FLOOR f
            INNER JOIN PHYSICAL_SHELF s ON f.ShelfId = s.Id AND NVL(s.IS_DELETED, 0) = 0
            WHERE NVL(f.IS_DELETED, 0) = 0";
        var parameters = new DynamicParameters();
        if (unitIds != null)
        {
            var ids = unitIds.Distinct().ToArray();
            if (ids.Length == 0)
                return Enumerable.Empty<PhysicalFloor>();
            sql += " AND s.UnitId IN :UnitIds";
            parameters.Add("UnitIds", ids);
        }
        sql += " ORDER BY NVL(f.PRIORITY, 1), f.Code, f.Id";
        return await _connection.QueryAsync<PhysicalFloor>(sql, parameters);
    }

    public async Task<PhysicalFloor?> GetFloorByIdAsync(long id)
    {
        return await _connection.QuerySingleOrDefaultAsync<PhysicalFloor>(
            $"SELECT * FROM PHYSICAL_FLOOR WHERE {nameof(PhysicalFloor.Id)} = :Id AND NVL(IS_DELETED, 0) = 0",
            new { Id = id });
    }

    public async Task<long> CreateFloorAsync(PhysicalFloor floor)
    {
        var sql = $@"INSERT INTO PHYSICAL_FLOOR (
                        {nameof(PhysicalFloor.ShelfId)}, 
                        {nameof(PhysicalFloor.Code)}, 
                        {nameof(PhysicalFloor.Name)}, 
                        {nameof(PhysicalFloor.Description)}, 
                        {nameof(PhysicalFloor.Priority)},
                        {nameof(PhysicalFloor.CreatedBy)}
                    ) 
                    VALUES (:ShelfId, :Code, :Name, :Description, NVL(:Priority, 1), :CreatedBy) 
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
                        {nameof(PhysicalFloor.Priority)} = NVL(:Priority, 1),
                        {nameof(PhysicalFloor.UpdatedAt)} = CURRENT_TIMESTAMP, 
                        {nameof(PhysicalFloor.UpdatedBy)} = :UpdatedBy 
                    WHERE {nameof(PhysicalFloor.Id)} = :Id AND NVL(IS_DELETED, 0) = 0";
        return await _connection.ExecuteAsync(sql, floor) > 0;
    }

    public async Task<bool> DeleteFloorAsync(long id)
    {
        return await _connection.ExecuteAsync(
            @"UPDATE PHYSICAL_FLOOR SET IS_DELETED = 1, UpdatedAt = CURRENT_TIMESTAMP
              WHERE Id = :Id AND NVL(IS_DELETED, 0) = 0",
            new { Id = id }) > 0;
    }

    // Box
    public async Task<IEnumerable<PhysicalBox>> GetBoxesByFloorIdAsync(long floorId)
    {
        return await _connection.QueryAsync<PhysicalBox>(
            $"SELECT * FROM PHYSICAL_BOX WHERE {nameof(PhysicalBox.FloorId)} = :FloorId AND NVL(IS_DELETED, 0) = 0 ORDER BY {nameof(PhysicalBox.Id)}",
            new { FloorId = floorId });
    }

    public async Task<IEnumerable<PhysicalBox>> GetBoxesByUnitIdsAsync(IEnumerable<long>? unitIds = null)
    {
        var sql = @"
            SELECT b.Id, b.FloorId, b.Code, b.Name, b.Description,
                   NVL(b.STATUS, 1) AS Status,
                   NVL(b.IS_DELETED, 0) AS IsDeleted,
                   NVL(b.PRIORITY, 1) AS Priority,
                   b.CreatedAt, b.CreatedBy, b.UpdatedAt, b.UpdatedBy
            FROM PHYSICAL_BOX b
            INNER JOIN PHYSICAL_FLOOR f ON b.FloorId = f.Id AND NVL(f.IS_DELETED, 0) = 0
            INNER JOIN PHYSICAL_SHELF s ON f.ShelfId = s.Id AND NVL(s.IS_DELETED, 0) = 0
            WHERE NVL(b.IS_DELETED, 0) = 0";
        var parameters = new DynamicParameters();
        if (unitIds != null)
        {
            var ids = unitIds.Distinct().ToArray();
            if (ids.Length == 0)
                return Enumerable.Empty<PhysicalBox>();
            sql += " AND s.UnitId IN :UnitIds";
            parameters.Add("UnitIds", ids);
        }
        sql += " ORDER BY NVL(b.PRIORITY, 1), b.Code, b.Id";
        return await _connection.QueryAsync<PhysicalBox>(sql, parameters);
    }

    public async Task<PhysicalBox?> GetBoxByIdAsync(long id)
    {
        return await _connection.QuerySingleOrDefaultAsync<PhysicalBox>(
            $"SELECT * FROM PHYSICAL_BOX WHERE {nameof(PhysicalBox.Id)} = :Id AND NVL(IS_DELETED, 0) = 0",
            new { Id = id });
    }

    public async Task<long> CreateBoxAsync(PhysicalBox box)
    {
        var sql = $@"INSERT INTO PHYSICAL_BOX (
                        {nameof(PhysicalBox.FloorId)}, 
                        {nameof(PhysicalBox.Code)}, 
                        {nameof(PhysicalBox.Name)}, 
                        {nameof(PhysicalBox.Description)}, 
                        {nameof(PhysicalBox.Priority)},
                        {nameof(PhysicalBox.CreatedBy)}
                    ) 
                    VALUES (:FloorId, :Code, :Name, :Description, NVL(:Priority, 1), :CreatedBy) 
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
                        {nameof(PhysicalBox.Priority)} = NVL(:Priority, 1),
                        {nameof(PhysicalBox.UpdatedAt)} = CURRENT_TIMESTAMP, 
                        {nameof(PhysicalBox.UpdatedBy)} = :UpdatedBy 
                    WHERE {nameof(PhysicalBox.Id)} = :Id AND NVL(IS_DELETED, 0) = 0";
        return await _connection.ExecuteAsync(sql, box) > 0;
    }

    public async Task<bool> DeleteBoxAsync(long id)
    {
        return await _connection.ExecuteAsync(
            @"UPDATE PHYSICAL_BOX SET IS_DELETED = 1, UpdatedAt = CURRENT_TIMESTAMP
              WHERE Id = :Id AND NVL(IS_DELETED, 0) = 0",
            new { Id = id }) > 0;
    }

    // ─────────────────── Sinh mã tự động ───────────────────

    public async Task<string?> GetUnitCodeAsync(long unitId)
    {
        return await _connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT Code FROM ORGANIZATION_UNIT WHERE Id = :UnitId AND NVL(IsDeleted, 0) = 0",
            new { UnitId = unitId });
    }

    public async Task<long?> FindUnitIdByCodeAsync(string code)
    {
        return await _connection.QuerySingleOrDefaultAsync<long?>(
            "SELECT Id FROM ORGANIZATION_UNIT WHERE UPPER(TRIM(Code)) = UPPER(TRIM(:Code)) AND NVL(IsDeleted, 0) = 0",
            new { Code = code });
    }

    /// <summary>Số thứ tự kế tiếp = MAX(số ở cuối Code khớp tiền tố) + 1, theo đúng phạm vi SQL truyền vào.</summary>
    private async Task<int> GetNextSequenceAsync(string sql, object parameters)
    {
        var max = await _connection.ExecuteScalarAsync<int>(sql, parameters);
        return max + 1;
    }

    public async Task<string?> GenerateNextShelfCodeAsync(long unitId)
    {
        var unitCode = await GetUnitCodeAsync(unitId);
        if (string.IsNullOrWhiteSpace(unitCode)) return null;

        var prefix = $"{unitCode.Trim()}_KE";
        const string sql = @"
            SELECT NVL(MAX(TO_NUMBER(REGEXP_SUBSTR(Code, '[0-9]+$'))), 0)
            FROM PHYSICAL_SHELF
            WHERE UnitId = :UnitId AND NVL(IS_DELETED, 0) = 0
              AND REGEXP_LIKE(Code, '^' || :Prefix || '[0-9]+$', 'i')";
        var next = await GetNextSequenceAsync(sql, new { UnitId = unitId, Prefix = prefix });
        return $"{prefix}{next}";
    }

    public async Task<string?> GenerateNextFloorCodeAsync(long unitId)
    {
        var unitCode = await GetUnitCodeAsync(unitId);
        if (string.IsNullOrWhiteSpace(unitCode)) return null;

        var prefix = $"{unitCode.Trim()}_TANG";
        const string sql = @"
            SELECT NVL(MAX(TO_NUMBER(REGEXP_SUBSTR(f.Code, '[0-9]+$'))), 0)
            FROM PHYSICAL_FLOOR f
            INNER JOIN PHYSICAL_SHELF s ON f.ShelfId = s.Id
            WHERE s.UnitId = :UnitId AND NVL(f.IS_DELETED, 0) = 0
              AND UPPER(f.Code) LIKE UPPER(:Prefix) || '%'
              AND REGEXP_LIKE(f.Code, '^' || :Prefix || '[0-9]+$', 'i')";
        var next = await GetNextSequenceAsync(sql, new { UnitId = unitId, Prefix = prefix });
        return $"{prefix}{next}";
    }

    public async Task<string?> GenerateNextBoxCodeAsync(long unitId)
    {
        var unitCode = await GetUnitCodeAsync(unitId);
        if (string.IsNullOrWhiteSpace(unitCode)) return null;

        var prefix = $"{unitCode.Trim()}_HOP";
        const string sql = @"
            SELECT NVL(MAX(TO_NUMBER(REGEXP_SUBSTR(b.Code, '[0-9]+$'))), 0)
            FROM PHYSICAL_BOX b
            INNER JOIN PHYSICAL_FLOOR f ON b.FloorId = f.Id
            INNER JOIN PHYSICAL_SHELF s ON f.ShelfId = s.Id
            WHERE s.UnitId = :UnitId AND NVL(b.IS_DELETED, 0) = 0
              AND UPPER(b.Code) LIKE UPPER(:Prefix) || '%'
              AND REGEXP_LIKE(b.Code, '^' || :Prefix || '[0-9]+$', 'i')";
        var next = await GetNextSequenceAsync(sql, new { UnitId = unitId, Prefix = prefix });
        return $"{prefix}{next}";
    }

    public async Task<long?> FindShelfIdByUnitAndNameAsync(long unitId, string name)
    {
        return await _connection.QuerySingleOrDefaultAsync<long?>(
            @"SELECT Id FROM PHYSICAL_SHELF
              WHERE UnitId = :UnitId AND NVL(IS_DELETED, 0) = 0
                AND UPPER(TRIM(Name)) = UPPER(TRIM(:Name))
              FETCH FIRST 1 ROW ONLY",
            new { UnitId = unitId, Name = name });
    }

    public async Task<long?> FindFloorIdByShelfAndNameAsync(long shelfId, string name)
    {
        return await _connection.QuerySingleOrDefaultAsync<long?>(
            @"SELECT Id FROM PHYSICAL_FLOOR
              WHERE ShelfId = :ShelfId AND NVL(IS_DELETED, 0) = 0
                AND UPPER(TRIM(Name)) = UPPER(TRIM(:Name))
              FETCH FIRST 1 ROW ONLY",
            new { ShelfId = shelfId, Name = name });
    }
}
