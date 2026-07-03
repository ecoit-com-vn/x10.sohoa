using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly IDbConnection _connection;

    public EquipmentRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<Equipment?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT Id, 
                            EquipmentTypeId, 
                            Name, 
                            Code, 
                            SerialNumber, 
                            INFRASTRUCTURE_ID as {nameof(Equipment.InfrastructureId)}, 
                            COUNTRY_ID as {nameof(Equipment.CountryId)}, 
                            IS_ACTIVE as {nameof(Equipment.IsActive)}, 
                            CreatorId as {nameof(Equipment.CreatorId)}, 
                            CreatedBy, 
                            CreatedAt, 
                            ModifiedBy as {nameof(Equipment.ModifiedBy)}, 
                            ModifiedDate as {nameof(Equipment.ModifiedDate)}, 
                            IsDeleted as {nameof(Equipment.IsDeleted)}, 
                            UnitId as {nameof(Equipment.UnitId)},
                            FORM_VALUES as {nameof(Equipment.FormValues)}
                     FROM EQUIPMENTS 
                     WHERE Id = :Id AND IsDeleted = 0";
        return await _connection.QuerySingleOrDefaultAsync<Equipment>(sql, new { Id = id.ToString() });
    }

    public async Task<Equipment?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        // UNIQUE trên cột Code áp dụng toàn bộ bảng EQUIPMENTS (kể cả bản ghi soft-delete).
        var sql = $@"SELECT Id, Code
                     FROM EQUIPMENTS
                     WHERE Code = :Code";

        return await _connection.QuerySingleOrDefaultAsync<Equipment>(
            sql,
            new { Code = code.Trim() });
    }

    public async Task<EquipmentDto?> GetDtoByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT e.Id AS {nameof(EquipmentDto.Id)},
                            e.Name AS {nameof(EquipmentDto.Name)},
                            e.Code AS {nameof(EquipmentDto.Code)},
                            e.SerialNumber AS {nameof(EquipmentDto.SerialNumber)},
                            e.EquipmentTypeId AS {nameof(EquipmentDto.EquipmentTypeId)},
                            e.INFRASTRUCTURE_ID AS {nameof(EquipmentDto.InfrastructureId)},
                            e.COUNTRY_ID AS {nameof(EquipmentDto.CountryId)},
                            e.IS_ACTIVE AS {nameof(EquipmentDto.IsActive)},
                            e.CreatedBy AS {nameof(EquipmentDto.CreatedBy)},
                            e.CreatedAt AS {nameof(EquipmentDto.CreatedAt)},
                            e.ModifiedBy AS {nameof(EquipmentDto.ModifiedBy)},
                            e.ModifiedDate AS {nameof(EquipmentDto.ModifiedDate)},
                            e.FORM_VALUES AS {nameof(EquipmentDto.FormValues)},
                            et.Name AS {nameof(EquipmentDto.EquipmentTypeName)},
                            et.Code AS {nameof(EquipmentDto.EquipmentTypeCode)},
                            et.GridTypeId AS {nameof(EquipmentDto.GridTypeId)},
                            gt.Name AS {nameof(EquipmentDto.GridTypeName)},
                            inf.Name AS {nameof(EquipmentDto.InfrastructureName)},
                            inf.Code AS {nameof(EquipmentDto.InfrastructureCode)},
                            e.UnitId AS {nameof(EquipmentDto.UnitId)},
                            u.Name AS {nameof(EquipmentDto.UnitName)},
                            c.Name AS {nameof(EquipmentDto.CountryName)},
                            c.Code AS {nameof(EquipmentDto.CountryCode)},
                            eft.Name AS {nameof(EquipmentDto.FormTemplateName)},
                            eft.FormSchema AS {nameof(EquipmentDto.FormSchema)},
                            usr.Id AS CreatorId,
                            usr.UserName AS Username,
                            usr.FullName AS FullName
                     FROM EQUIPMENTS e
                     LEFT JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                     LEFT JOIN GridTypes gt ON et.GridTypeId = gt.Id
                     LEFT JOIN INFRASTRUCTURE inf ON e.INFRASTRUCTURE_ID = inf.Id
                     LEFT JOIN ORGANIZATION_UNIT u ON e.UnitId = u.Id
                     LEFT JOIN COUNTRIES c ON e.COUNTRY_ID = c.Id
                      LEFT JOIN (
                          SELECT * FROM (
                              SELECT Id, Name, FormSchema, EquipmentTypeId,
                                     ROW_NUMBER() OVER (
                                         PARTITION BY EquipmentTypeId 
                                         ORDER BY CASE WHEN Status = 'Hoàn thành' THEN 0 ELSE 1 END, Version DESC
                                     ) as rn
                              FROM EavFormTemplates
                              WHERE IsDeleted = 0
                                AND IsActive = 1
                                AND FormType = 'TEMPLATE'
                          ) WHERE rn = 1
                      ) eft ON e.EquipmentTypeId = eft.EquipmentTypeId
                     LEFT JOIN APP_USER usr ON e.CreatorId = usr.Id
                     WHERE e.Id = :Id AND e.IsDeleted = 0";

        var result = await _connection.QueryAsync<EquipmentDto, CreatorInfoRow, EquipmentDto>(
            sql, 
            (eq, creatorRow) => {
                eq.Creator = creatorRow?.ToDto();
                return eq;
            },
            new { Id = id.ToString() },
            splitOn: "CreatorId"
        );
        return result.FirstOrDefault();
    }

    public async Task<IEnumerable<Equipment>> GetAllAsync(IEnumerable<long>? unitIds = null)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        if (unitIds == null || !unitIds.Any())
        {
            var sql = $@"SELECT Id, 
                                EquipmentTypeId, 
                                Name, 
                                Code, 
                                SerialNumber, 
                                INFRASTRUCTURE_ID as {nameof(Equipment.InfrastructureId)}, 
                                COUNTRY_ID as {nameof(Equipment.CountryId)}, 
                                IS_ACTIVE as {nameof(Equipment.IsActive)}, 
                                CreatorId as {nameof(Equipment.CreatorId)}, 
                                CreatedBy, 
                                CreatedAt, 
                                ModifiedBy as {nameof(Equipment.ModifiedBy)}, 
                                ModifiedDate as {nameof(Equipment.ModifiedDate)}, 
                                IsDeleted as {nameof(Equipment.IsDeleted)}, 
                                UnitId as {nameof(Equipment.UnitId)}
                         FROM EQUIPMENTS WHERE IsDeleted = 0";
            return await _connection.QueryAsync<Equipment>(sql);
        }
        else
        {
            var sql = $@"SELECT Id, 
                                EquipmentTypeId, 
                                Name, 
                                Code, 
                                SerialNumber, 
                                INFRASTRUCTURE_ID as {nameof(Equipment.InfrastructureId)}, 
                                COUNTRY_ID as {nameof(Equipment.CountryId)}, 
                                IS_ACTIVE as {nameof(Equipment.IsActive)}, 
                                CreatorId as {nameof(Equipment.CreatorId)}, 
                                CreatedBy, 
                                CreatedAt, 
                                ModifiedBy as {nameof(Equipment.ModifiedBy)}, 
                                ModifiedDate as {nameof(Equipment.ModifiedDate)}, 
                                IsDeleted as {nameof(Equipment.IsDeleted)}, 
                                UnitId as {nameof(Equipment.UnitId)}
                         FROM EQUIPMENTS 
                         WHERE UnitId IN :UnitIds AND IsDeleted = 0";
            return await _connection.QueryAsync<Equipment>(sql, new { UnitIds = unitIds.ToArray() });
        }
    }

    public async Task<(IEnumerable<EquipmentDto> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        string? code, 
        string? name, 
        long? unitId, 
        Guid? infrastructureId, 
        int? gridTypeId, 
        Guid? equipmentTypeId, 
        bool? isActive, 
        IEnumerable<long>? authorizedUnitIds)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sqlBase = @"FROM EQUIPMENTS e
                        LEFT JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                        LEFT JOIN GridTypes gt ON et.GridTypeId = gt.Id
                        LEFT JOIN INFRASTRUCTURE inf ON e.INFRASTRUCTURE_ID = inf.Id
                        LEFT JOIN ORGANIZATION_UNIT u ON e.UnitId = u.Id
                        LEFT JOIN COUNTRIES c ON e.COUNTRY_ID = c.Id
                        LEFT JOIN APP_USER usr ON e.CreatorId = usr.Id
                        WHERE e.IsDeleted = 0";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(code))
        {
            sqlBase += " AND LOWER(e.Code) LIKE :Code";
            parameters.Add("Code", $"%{code.ToLower().Trim()}%");
        }

        if (!string.IsNullOrEmpty(name))
        {
            sqlBase += " AND LOWER(e.Name) LIKE :Name";
            parameters.Add("Name", $"%{name.ToLower().Trim()}%");
        }

        if (unitId.HasValue)
        {
            sqlBase += " AND e.UnitId = :UnitId";
            parameters.Add("UnitId", unitId.Value);
        }
        else if (authorizedUnitIds != null && authorizedUnitIds.Any())
        {
            sqlBase += " AND e.UnitId IN :AuthorizedUnitIds";
            parameters.Add("AuthorizedUnitIds", authorizedUnitIds.ToArray());
        }

        if (infrastructureId.HasValue)
        {
            sqlBase += " AND e.INFRASTRUCTURE_ID = :InfrastructureId";
            parameters.Add("InfrastructureId", infrastructureId.Value.ToString());
        }

        if (gridTypeId.HasValue)
        {
            sqlBase += " AND et.GridTypeId = :GridTypeId";
            parameters.Add("GridTypeId", gridTypeId.Value);
        }

        if (equipmentTypeId.HasValue)
        {
            sqlBase += " AND e.EquipmentTypeId = :EquipmentTypeId";
            parameters.Add("EquipmentTypeId", equipmentTypeId.Value.ToString());
        }

        if (isActive.HasValue)
        {
            sqlBase += " AND e.IS_ACTIVE = :IsActive";
            parameters.Add("IsActive", isActive.Value ? 1 : 0);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectSql = $@"SELECT e.Id AS {nameof(EquipmentDto.Id)},
                                   e.Name AS {nameof(EquipmentDto.Name)},
                                   e.Code AS {nameof(EquipmentDto.Code)},
                                   e.SerialNumber AS {nameof(EquipmentDto.SerialNumber)},
                                   e.EquipmentTypeId AS {nameof(EquipmentDto.EquipmentTypeId)},
                                   e.INFRASTRUCTURE_ID AS {nameof(EquipmentDto.InfrastructureId)},
                                   e.COUNTRY_ID AS {nameof(EquipmentDto.CountryId)},
                                   e.IS_ACTIVE AS {nameof(EquipmentDto.IsActive)},
                                   e.CreatedBy AS {nameof(EquipmentDto.CreatedBy)},
                                   e.CreatedAt AS {nameof(EquipmentDto.CreatedAt)},
                                   e.ModifiedBy AS {nameof(EquipmentDto.ModifiedBy)},
                                   e.ModifiedDate AS {nameof(EquipmentDto.ModifiedDate)},
                                   et.Name AS {nameof(EquipmentDto.EquipmentTypeName)},
                                   et.Code AS {nameof(EquipmentDto.EquipmentTypeCode)},
                                   et.GridTypeId AS {nameof(EquipmentDto.GridTypeId)},
                                   gt.Name AS {nameof(EquipmentDto.GridTypeName)},
                                   inf.Name AS {nameof(EquipmentDto.InfrastructureName)},
                                   inf.Code AS {nameof(EquipmentDto.InfrastructureCode)},
                                   e.UnitId AS {nameof(EquipmentDto.UnitId)},
                                   u.Name AS {nameof(EquipmentDto.UnitName)},
                                   c.Name AS {nameof(EquipmentDto.CountryName)},
                                   c.Code AS {nameof(EquipmentDto.CountryCode)},
                                   usr.Id AS CreatorId,
                                   usr.UserName AS Username,
                                   usr.FullName AS FullName
                            {sqlBase}
                            ORDER BY e.CreatedAt DESC, e.Code ASC
                            OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var items = await _connection.QueryAsync<EquipmentDto, CreatorInfoRow, EquipmentDto>(
            selectSql,
            (eq, creatorRow) => {
                eq.Creator = creatorRow?.ToDto();
                return eq;
            },
            parameters,
            splitOn: "CreatorId"
        );

        return (items, totalCount);
    }

    public async Task<(IEnumerable<EquipmentLookupItemDto> Items, int TotalCount)> GetLookupPagedAsync(
        EquipmentLookupFilterDto filter,
        IEnumerable<long>? authorizedUnitIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize < 1) filter.PageSize = 10;

        var sqlBase = @"FROM EQUIPMENTS e
                        LEFT JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                        LEFT JOIN INFRASTRUCTURE inf ON e.INFRASTRUCTURE_ID = inf.Id
                        WHERE e.IsDeleted = 0";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            sqlBase += " AND (LOWER(e.Code) LIKE :Keyword OR LOWER(e.Name) LIKE :Keyword)";
            parameters.Add("Keyword", $"%{filter.Keyword.ToLower().Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Code))
        {
            sqlBase += " AND LOWER(e.Code) LIKE :Code";
            parameters.Add("Code", $"%{filter.Code.ToLower().Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            sqlBase += " AND LOWER(e.Name) LIKE :Name";
            parameters.Add("Name", $"%{filter.Name.ToLower().Trim()}%");
        }

        if (filter.UnitId.HasValue)
        {
            sqlBase += " AND e.UnitId = :UnitId";
            parameters.Add("UnitId", filter.UnitId.Value);
        }
        else if (authorizedUnitIds != null && authorizedUnitIds.Any())
        {
            sqlBase += " AND e.UnitId IN :AuthorizedUnitIds";
            parameters.Add("AuthorizedUnitIds", authorizedUnitIds.ToArray());
        }

        if (filter.InfrastructureId.HasValue)
        {
            sqlBase += " AND e.INFRASTRUCTURE_ID = :InfrastructureId";
            parameters.Add("InfrastructureId", filter.InfrastructureId.Value.ToString());
        }

        if (filter.GridTypeId.HasValue)
        {
            sqlBase += " AND et.GridTypeId = :GridTypeId";
            parameters.Add("GridTypeId", filter.GridTypeId.Value);
        }

        if (filter.IsActive.HasValue)
        {
            sqlBase += " AND e.IS_ACTIVE = :IsActive";
            parameters.Add("IsActive", filter.IsActive.Value ? 1 : 0);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectSql = $@"SELECT e.Id AS {nameof(EquipmentLookupItemDto.Id)},
                                   e.Code AS {nameof(EquipmentLookupItemDto.Code)},
                                   e.Name AS {nameof(EquipmentLookupItemDto.Name)},
                                   inf.Name AS {nameof(EquipmentLookupItemDto.InfrastructureName)}
                            {sqlBase}
                            ORDER BY e.Code ASC
                            OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);
        parameters.Add("PageSize", filter.PageSize);

        var items = await _connection.QueryAsync<EquipmentLookupItemDto>(selectSql, parameters);
        return (items, totalCount);
    }

    public async Task<bool> CreateWithAttributesAsync(Equipment equipment, IEnumerable<AttributeValue> attributes)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
        using var transaction = _connection.BeginTransaction();

        try
        {
            var insertEquipmentSql = $@"INSERT INTO EQUIPMENTS (
                                           Id, 
                                           EquipmentTypeId, 
                                           Name, 
                                           Code, 
                                           SerialNumber, 
                                           INFRASTRUCTURE_ID, 
                                           COUNTRY_ID, 
                                           IS_ACTIVE, 
                                           CreatorId, 
                                           CreatedBy, 
                                           CreatedAt, 
                                           IsDeleted,
                                           UnitId,
                                           FORM_VALUES
                                       )
                                       VALUES (
                                           :Id, 
                                           :EquipmentTypeId, 
                                           :Name, 
                                           :Code, 
                                           :SerialNumber, 
                                           :InfrastructureId, 
                                           :CountryId, 
                                           :IsActive, 
                                           :CreatorId, 
                                           :CreatedBy, 
                                           :CreatedAt, 
                                           0,
                                           :UnitId,
                                           :FormValues
                                       )";

            var param = new
            {
                Id = equipment.Id.ToString(),
                EquipmentTypeId = equipment.EquipmentTypeId.ToString(),
                equipment.Name,
                equipment.Code,
                equipment.SerialNumber,
                InfrastructureId = equipment.InfrastructureId?.ToString(),
                CountryId = equipment.CountryId?.ToString(),
                IsActive = equipment.IsActive ? 1 : 0,
                CreatorId = equipment.CreatorId?.ToString(),
                equipment.CreatedBy,
                equipment.CreatedAt,
                equipment.UnitId,
                equipment.FormValues
            };

            await _connection.ExecuteAsync(insertEquipmentSql, param, transaction);

            if (attributes != null && attributes.Any())
            {
                var insertAttributeSql = $@"INSERT INTO AttributeValues (
                                               Id, 
                                               EquipmentId, 
                                               AttributeDefinitionId, 
                                               Value
                                           )
                                           VALUES (:Id, :EquipmentId, :AttributeDefinitionId, :Value)";
                var attrParams = attributes.Select(a => new
                {
                    Id = a.Id.ToString(),
                    EquipmentId = a.EquipmentId.ToString(),
                    AttributeDefinitionId = a.AttributeDefinitionId.ToString(),
                    a.Value
                });
                await _connection.ExecuteAsync(insertAttributeSql, attrParams, transaction);
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> CreateAsync(Equipment equipment)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"INSERT INTO EQUIPMENTS (
                        Id, 
                        EquipmentTypeId, 
                        Name, 
                        Code, 
                        SerialNumber, 
                        INFRASTRUCTURE_ID, 
                        COUNTRY_ID, 
                        IS_ACTIVE, 
                        CreatorId, 
                        CreatedBy, 
                        CreatedAt, 
                        IsDeleted,
                        UnitId,
                        FORM_VALUES
                    )
                    VALUES (
                        :Id, 
                        :EquipmentTypeId, 
                        :Name, 
                        :Code, 
                        :SerialNumber, 
                        :InfrastructureId, 
                        :CountryId, 
                        :IsActive, 
                        :CreatorId, 
                        :CreatedBy, 
                        :CreatedAt, 
                        0,
                        :UnitId,
                        :FormValues
                    )";

        var param = new
        {
            Id = equipment.Id.ToString(),
            EquipmentTypeId = equipment.EquipmentTypeId.ToString(),
            equipment.Name,
            equipment.Code,
            equipment.SerialNumber,
            InfrastructureId = equipment.InfrastructureId?.ToString(),
            CountryId = equipment.CountryId?.ToString(),
            IsActive = equipment.IsActive ? 1 : 0,
            CreatorId = equipment.CreatorId?.ToString(),
            equipment.CreatedBy,
            equipment.CreatedAt,
            equipment.UnitId,
            equipment.FormValues
        };

        var result = await _connection.ExecuteAsync(sql, param);
        return result > 0;
    }

    public async Task<bool> UpdateAsync(Equipment equipment)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"UPDATE EQUIPMENTS 
                    SET EquipmentTypeId = :EquipmentTypeId,
                        Name = :Name,
                        Code = :Code,
                        SerialNumber = :SerialNumber,
                        INFRASTRUCTURE_ID = :InfrastructureId,
                        COUNTRY_ID = :CountryId,
                        IS_ACTIVE = :IsActive,
                        ModifiedBy = :ModifiedBy,
                        ModifiedDate = :ModifiedDate,
                        UnitId = :UnitId,
                        FORM_VALUES = :FormValues
                    WHERE Id = :Id AND IsDeleted = 0";

        var param = new
        {
            Id = equipment.Id.ToString(),
            EquipmentTypeId = equipment.EquipmentTypeId.ToString(),
            equipment.Name,
            equipment.Code,
            equipment.SerialNumber,
            InfrastructureId = equipment.InfrastructureId?.ToString(),
            CountryId = equipment.CountryId?.ToString(),
            IsActive = equipment.IsActive ? 1 : 0,
            equipment.ModifiedBy,
            ModifiedDate = DateTime.UtcNow,
            equipment.UnitId,
            equipment.FormValues
        };

        var result = await _connection.ExecuteAsync(sql, param);
        return result > 0;
    }

    public async Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
        using var transaction = _connection.BeginTransaction();
        try
        {
            var deleteSql = "DELETE FROM AttributeValues WHERE EquipmentId = :EquipmentId";
            await _connection.ExecuteAsync(deleteSql, new { EquipmentId = equipmentId.ToString() }, transaction);

            if (attributes != null && attributes.Any())
            {
                var insertSql = $@"INSERT INTO AttributeValues (
                                      Id, 
                                      EquipmentId, 
                                      AttributeDefinitionId, 
                                      Value
                                  )
                                  VALUES (:Id, :EquipmentId, :AttributeDefinitionId, :Value)";
                var attrParams = attributes.Select(a => new
                {
                    Id = a.Id.ToString(),
                    EquipmentId = a.EquipmentId.ToString(),
                    AttributeDefinitionId = a.AttributeDefinitionId.ToString(),
                    a.Value
                });
                await _connection.ExecuteAsync(insertSql, attrParams, transaction);
            }
            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = "UPDATE EQUIPMENTS SET IsDeleted = 1, ModifiedDate = :ModifiedDate WHERE Id = :Id";
        var result = await _connection.ExecuteAsync(sql, new { Id = id.ToString(), ModifiedDate = DateTime.UtcNow });
        return result > 0;
    }

    public async Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = "SELECT * FROM AttributeValues WHERE EquipmentId = :EquipmentId";
        return await _connection.QueryAsync<AttributeValue>(sql, new { EquipmentId = equipmentId.ToString() });
    }

    // Lookups
    public async Task<IEnumerable<Country>> GetCountriesAsync()
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = "SELECT ID, CODE, NAME FROM COUNTRIES ORDER BY NAME ASC";
        return await _connection.QueryAsync<Country>(sql);
    }

    public async Task<IEnumerable<OrganizationDto>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        if (startUnitId.HasValue)
        {
            var sql = @"SELECT Id, Code, Name, ParentId 
                        FROM ORGANIZATION_UNIT
                        START WITH Id = :StartUnitId
                        CONNECT BY PRIOR Id = ParentId";
            return await _connection.QueryAsync<OrganizationDto>(sql, new { StartUnitId = startUnitId.Value });
        }
        else
        {
            var sql = "SELECT Id, Code, Name, ParentId FROM ORGANIZATION_UNIT ORDER BY Name ASC";
            return await _connection.QueryAsync<OrganizationDto>(sql);
        }
    }

    public async Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(IEnumerable<long>? authorizedUnitIds = null)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = @"SELECT ID, CODE, NAME, INFRA_TYPE_ID as InfraTypeId, UNIT_ID as UnitId, GRIDTYPEID as GridTypeId, IS_ACTIVE as IsActive 
                    FROM INFRASTRUCTURE 
                    WHERE IsDeleted = 0";

        var parameters = new DynamicParameters();
        if (authorizedUnitIds != null && authorizedUnitIds.Any())
        {
            sql += " AND UNIT_ID IN :AuthorizedUnitIds";
            parameters.Add("AuthorizedUnitIds", authorizedUnitIds.ToArray());
        }

        sql += " ORDER BY NAME ASC";
        return await _connection.QueryAsync<InfrastructureEntity>(sql, parameters);
    }

    public async Task<IEnumerable<EquipmentTypeDto>> GetEquipmentTypesLookupAsync()
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = @"SELECT et.Id, et.Code, et.Name, et.GridTypeId, gt.Name as GridTypeName, et.IsActive 
                    FROM EquipmentTypes et
                    LEFT JOIN GridTypes gt ON et.GridTypeId = gt.Id
                    WHERE et.IsDeleted = 0 
                    ORDER BY et.Name ASC";
        return await _connection.QueryAsync<EquipmentTypeDto>(sql);
    }
}
