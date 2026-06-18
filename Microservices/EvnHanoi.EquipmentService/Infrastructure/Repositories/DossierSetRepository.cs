using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DossierSetRepository : IDossierSetRepository
{
    private readonly IDbConnection _connection;

    public DossierSetRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<IEnumerable<DossierSetDto>> GetAllAsync(long? unitId = null)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT
                        {nameof(DossierSet.Id)},
                        {nameof(DossierSet.Code)},
                        {nameof(DossierSet.Name)},
                        {nameof(DossierSet.UnitId)},
                        {nameof(DossierSet.CreatedBy)},
                        {nameof(DossierSet.CreatedDate)}
                     FROM DOSSIER_SETS
                     WHERE {nameof(DossierSet.IsDeleted)} = 0";

        var parameters = new DynamicParameters();
        if (unitId.HasValue)
        {
            sql += $" AND {nameof(DossierSet.UnitId)} = :UnitId";
            parameters.Add("UnitId", unitId.Value);
        }

        sql += $" ORDER BY {nameof(DossierSet.Name)} ASC";

        return await _connection.QueryAsync<DossierSetDto>(sql, parameters);
    }

    public async Task<DossierSet?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $"SELECT * FROM DOSSIER_SETS WHERE {nameof(DossierSet.Id)} = :Id AND {nameof(DossierSet.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<DossierSet>(sql, new { Id = id.ToString() });
    }

    public async Task<Guid> CreateAsync(DossierSet dossierSet)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        if (dossierSet.Id == Guid.Empty)
            dossierSet.Id = Guid.Parse(UuidHelper.NewUuid());

        var sql = $@"INSERT INTO DOSSIER_SETS (
                        {nameof(DossierSet.Id)},
                        {nameof(DossierSet.Code)},
                        {nameof(DossierSet.Name)},
                        {nameof(DossierSet.UnitId)},
                        {nameof(DossierSet.CreatedBy)},
                        {nameof(DossierSet.CreatedDate)},
                        {nameof(DossierSet.IsDeleted)}
                    ) VALUES (:Id, :Code, :Name, :UnitId, :CreatedBy, :CreatedDate, :IsDeleted)";

        await _connection.ExecuteAsync(sql, new
        {
            Id = dossierSet.Id.ToString(),
            dossierSet.Code,
            dossierSet.Name,
            dossierSet.UnitId,
            dossierSet.CreatedBy,
            dossierSet.CreatedDate,
            IsDeleted = dossierSet.IsDeleted ? 1 : 0
        });

        return dossierSet.Id;
    }

    public async Task<bool> UpdateAsync(DossierSet dossierSet)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"UPDATE DOSSIER_SETS SET
                        {nameof(DossierSet.Code)} = :Code,
                        {nameof(DossierSet.Name)} = :Name,
                        {nameof(DossierSet.UnitId)} = :UnitId,
                        {nameof(DossierSet.ModifiedBy)} = :ModifiedBy,
                        {nameof(DossierSet.ModifiedDate)} = :ModifiedDate
                     WHERE {nameof(DossierSet.Id)} = :Id AND {nameof(DossierSet.IsDeleted)} = 0";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = dossierSet.Id.ToString(),
            dossierSet.Code,
            dossierSet.Name,
            dossierSet.UnitId,
            dossierSet.ModifiedBy,
            dossierSet.ModifiedDate
        });
        return affected > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"UPDATE DOSSIER_SETS SET
                        {nameof(DossierSet.IsDeleted)} = 1,
                        {nameof(DossierSet.ModifiedBy)} = :ModifiedBy,
                        {nameof(DossierSet.ModifiedDate)} = :ModifiedDate
                     WHERE {nameof(DossierSet.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }
}
