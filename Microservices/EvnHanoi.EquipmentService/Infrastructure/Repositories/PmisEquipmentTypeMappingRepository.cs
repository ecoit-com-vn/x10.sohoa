using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class PmisEquipmentTypeMappingRepository : IPmisEquipmentTypeMappingRepository
{
    private readonly IDbConnection _connection;

    public PmisEquipmentTypeMappingRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<PmisEquipmentTypeMappingDto>> GetAllAsync()
    {
        EnsureOpen();

        const string sql = @"
            SELECT m.Id AS Id,
                   m.PmisMaLoaiTB AS PmisMaLoaiTB,
                   m.GridTypeId AS GridTypeId,
                   g.Name AS GridTypeName,
                   m.EquipmentTypeId AS EquipmentTypeId,
                   t.Code AS EquipmentTypeCode,
                   t.Name AS EquipmentTypeName,
                   m.RowVersion AS RowVersion
            FROM PMIS_EQUIPMENT_TYPE_MAPPING m
            LEFT JOIN GRIDTYPES g ON g.Id = m.GridTypeId
            LEFT JOIN EquipmentTypes t ON t.Id = m.EquipmentTypeId
            WHERE m.IsDeleted = 0
            ORDER BY m.PmisMaLoaiTB, m.GridTypeId";
        return await _connection.QueryAsync<PmisEquipmentTypeMappingDto>(sql);
    }

    public async Task<string> CreateAsync(SavePmisEquipmentTypeMappingRequest request, string? createdBy)
    {
        EnsureOpen();

        var id = Guid.CreateVersion7().ToString();
        const string sql = @"
            INSERT INTO PMIS_EQUIPMENT_TYPE_MAPPING (Id, PmisMaLoaiTB, GridTypeId, EquipmentTypeId, CreatedBy)
            VALUES (:Id, :PmisMaLoaiTB, :GridTypeId, :EquipmentTypeId, :CreatedBy)";
        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            request.PmisMaLoaiTB,
            request.GridTypeId,
            request.EquipmentTypeId,
            CreatedBy = createdBy
        });
        return id;
    }

    public async Task<bool> UpdateAsync(string id, SavePmisEquipmentTypeMappingRequest request, string? modifiedBy)
    {
        EnsureOpen();

        const string sql = @"
            UPDATE PMIS_EQUIPMENT_TYPE_MAPPING
            SET PmisMaLoaiTB = :PmisMaLoaiTB,
                GridTypeId = :GridTypeId,
                EquipmentTypeId = :EquipmentTypeId,
                RowVersion = RowVersion + 1,
                ModifiedBy = :ModifiedBy,
                ModifiedDate = SYSTIMESTAMP
            WHERE Id = :Id AND RowVersion = :ExpectedVersion AND IsDeleted = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            request.PmisMaLoaiTB,
            request.GridTypeId,
            request.EquipmentTypeId,
            ModifiedBy = modifiedBy,
            ExpectedVersion = request.RowVersion
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(string id, string? modifiedBy)
    {
        EnsureOpen();

        const string sql = @"
            UPDATE PMIS_EQUIPMENT_TYPE_MAPPING
            SET IsDeleted = 1,
                RowVersion = RowVersion + 1,
                ModifiedBy = :ModifiedBy,
                ModifiedDate = SYSTIMESTAMP
            WHERE Id = :Id AND IsDeleted = 0";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id, ModifiedBy = modifiedBy });
        return affected > 0;
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
