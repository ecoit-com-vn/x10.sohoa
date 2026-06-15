using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentTypeRepository : IEquipmentTypeRepository
{
    private readonly IDbConnection _connection;

    public EquipmentTypeRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<EquipmentTypeDto?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT et.Id AS {nameof(EquipmentTypeDto.Id)},
                            et.Code AS {nameof(EquipmentTypeDto.Code)},
                            et.Name AS {nameof(EquipmentTypeDto.Name)},
                            et.Description AS {nameof(EquipmentTypeDto.Description)},
                            et.GridTypeId AS {nameof(EquipmentTypeDto.GridTypeId)},
                            et.SortOrder AS {nameof(EquipmentTypeDto.SortOrder)},
                            et.IsActive AS {nameof(EquipmentTypeDto.IsActive)},
                            et.CreatedBy AS {nameof(EquipmentTypeDto.CreatedBy)},
                            et.CreatedAt AS {nameof(EquipmentTypeDto.CreatedAt)},
                            et.ModifiedBy AS {nameof(EquipmentTypeDto.ModifiedBy)},
                            et.UpdatedAt AS {nameof(EquipmentTypeDto.UpdatedAt)},
                            gt.Name AS {nameof(EquipmentTypeDto.GridTypeName)},
                            u.Id AS CreatorId,
                            u.UserName AS Username,
                            u.FullName AS Name
                     FROM EquipmentTypes et
                     LEFT JOIN GridTypes gt ON et.GridTypeId = gt.Id
                     LEFT JOIN APP_USER u ON et.CreatorId = u.Id
                     WHERE et.Id = :Id AND et.IsDeleted = 0";

        var result = await _connection.QueryAsync<EquipmentTypeDto, CreatorInfoDto, EquipmentTypeDto>(
            sql, 
            (eqType, creator) => {
                if (creator != null && creator.Id != Guid.Empty) {
                    eqType.Creator = creator;
                }
                return eqType;
            },
            new { Id = id.ToString() },
            splitOn: "CreatorId"
        );
        return result.FirstOrDefault();
    }

    public async Task<IEnumerable<EquipmentType>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = "SELECT * FROM EquipmentTypes WHERE IsDeleted = 0 ORDER BY SortOrder ASC, Code ASC";
        return await _connection.QueryAsync<EquipmentType>(sql);
    }

    public async Task<(IEnumerable<EquipmentTypeDto> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        string? code, 
        string? name, 
        int? gridTypeId, 
        bool? isActive)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sqlBase = @"FROM EquipmentTypes et
                        LEFT JOIN GridTypes gt ON et.GridTypeId = gt.Id
                        LEFT JOIN APP_USER u ON et.CreatorId = u.Id
                        WHERE et.IsDeleted = 0";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(code))
        {
            sqlBase += " AND LOWER(et.Code) LIKE :Code";
            parameters.Add("Code", $"%{code.ToLower().Trim()}%");
        }

        if (!string.IsNullOrEmpty(name))
        {
            sqlBase += " AND LOWER(et.Name) LIKE :Name";
            parameters.Add("Name", $"%{name.ToLower().Trim()}%");
        }

        if (gridTypeId.HasValue)
        {
            sqlBase += " AND et.GridTypeId = :GridTypeId";
            parameters.Add("GridTypeId", gridTypeId.Value);
        }

        if (isActive.HasValue)
        {
            sqlBase += " AND et.IsActive = :IsActive";
            parameters.Add("IsActive", isActive.Value ? 1 : 0);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectSql = $@"SELECT et.Id AS {nameof(EquipmentTypeDto.Id)},
                                   et.Code AS {nameof(EquipmentTypeDto.Code)},
                                   et.Name AS {nameof(EquipmentTypeDto.Name)},
                                   et.Description AS {nameof(EquipmentTypeDto.Description)},
                                   et.GridTypeId AS {nameof(EquipmentTypeDto.GridTypeId)},
                                   et.SortOrder AS {nameof(EquipmentTypeDto.SortOrder)},
                                   et.IsActive AS {nameof(EquipmentTypeDto.IsActive)},
                                   et.CreatedBy AS {nameof(EquipmentTypeDto.CreatedBy)},
                                   et.CreatedAt AS {nameof(EquipmentTypeDto.CreatedAt)},
                                   et.ModifiedBy AS {nameof(EquipmentTypeDto.ModifiedBy)},
                                   et.UpdatedAt AS {nameof(EquipmentTypeDto.UpdatedAt)},
                                   gt.Name AS {nameof(EquipmentTypeDto.GridTypeName)},
                                   u.Id AS CreatorId,
                                   u.UserName AS Username,
                                   u.FullName AS Name
                            {sqlBase}
                            ORDER BY et.SortOrder ASC, et.Code ASC, et.CreatedAt DESC
                            OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var items = await _connection.QueryAsync<EquipmentTypeDto, CreatorInfoDto, EquipmentTypeDto>(
            selectSql,
            (eqType, creator) => {
                if (creator != null && creator.Id != Guid.Empty) {
                    eqType.Creator = creator;
                }
                return eqType;
            },
            parameters,
            splitOn: "CreatorId"
        );

        return (items, totalCount);
    }

    public async Task<IEnumerable<GridType>> GetGridTypesAsync()
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = "SELECT Id, Name FROM GridTypes ORDER BY Id ASC";
        return await _connection.QueryAsync<GridType>(sql);
    }

    public async Task<bool> CreateAsync(EquipmentType type)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"INSERT INTO EquipmentTypes (
                        Id, 
                        Name, 
                        Code, 
                        Description, 
                        GridTypeId, 
                        SortOrder, 
                        IsActive, 
                        CreatorId, 
                        CreatedBy, 
                        CreatedAt, 
                        IsDeleted
                    )
                    VALUES (:Id, :Name, :Code, :Description, :GridTypeId, :SortOrder, :IsActive, :CreatorId, :CreatedBy, :CreatedAt, 0)";

        var param = new
        {
            Id = type.Id.ToString(),
            type.Name,
            type.Code,
            type.Description,
            type.GridTypeId,
            type.SortOrder,
            IsActive = type.IsActive ? 1 : 0,
            CreatorId = type.CreatorId?.ToString(),
            type.CreatedBy,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _connection.ExecuteAsync(sql, param);
        return result > 0;
    }

    public async Task<bool> UpdateAsync(EquipmentType type)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"UPDATE EquipmentTypes 
                    SET Name = :Name,
                        Code = :Code,
                        Description = :Description,
                        GridTypeId = :GridTypeId,
                        SortOrder = :SortOrder,
                        IsActive = :IsActive,
                        ModifiedBy = :ModifiedBy,
                        UpdatedAt = :UpdatedAt
                    WHERE Id = :Id AND IsDeleted = 0";

        var param = new
        {
            Id = type.Id.ToString(),
            type.Name,
            type.Code,
            type.Description,
            type.GridTypeId,
            type.SortOrder,
            IsActive = type.IsActive ? 1 : 0,
            type.ModifiedBy,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _connection.ExecuteAsync(sql, param);
        return result > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = "UPDATE EquipmentTypes SET IsDeleted = 1, UpdatedAt = :UpdatedAt WHERE Id = :Id";
        var result = await _connection.ExecuteAsync(sql, new { Id = id.ToString(), UpdatedAt = DateTime.UtcNow });
        return result > 0;
    }

    public async Task<IEnumerable<AttributeDefinition>> GetAttributeDefinitionsAsync(Guid equipmentTypeId)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = "SELECT * FROM AttributeDefinitions WHERE EquipmentTypeId = :EquipmentTypeId";
        return await _connection.QueryAsync<AttributeDefinition>(sql, new { EquipmentTypeId = equipmentTypeId.ToString() });
    }

    public async Task<bool> AddAttributeDefinitionAsync(AttributeDefinition attributeDefinition)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = @"INSERT INTO AttributeDefinitions (Id, EquipmentTypeId, Name, Code, DataType, IsRequired)
                    VALUES (:Id, :EquipmentTypeId, :Name, :Code, :DataType, :IsRequired)";

        var param = new
        {
            Id = attributeDefinition.Id.ToString(),
            EquipmentTypeId = attributeDefinition.EquipmentTypeId.ToString(),
            attributeDefinition.Name,
            attributeDefinition.Code,
            attributeDefinition.DataType,
            IsRequired = attributeDefinition.IsRequired ? 1 : 0
        };

        var result = await _connection.ExecuteAsync(sql, param);
        return result > 0;
    }
}
