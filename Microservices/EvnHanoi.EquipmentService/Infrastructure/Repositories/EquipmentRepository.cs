using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Database;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly IDbConnection _connection;
    private readonly IFileStorageService _fileStorageService;

    public EquipmentRepository(IDbConnection connection, IFileStorageService fileStorageService)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
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
                            MANUFACTURE_YEAR as {nameof(Equipment.ManufactureYear)},
                            EQUIPMENT_STATUS_ID as {nameof(Equipment.EquipmentStatusId)},
                            IS_ACTIVE as {nameof(Equipment.IsActive)},
                            StatusTransition as {nameof(Equipment.StatusTransition)},
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

    public async Task<Equipment?> GetByCodeAsync(string code, Guid? infrastructureId)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        // UNIQUE áp dụng cho cặp Code và InfrastructureId, kể cả bản ghi soft-delete.
        const string sql = @"SELECT Id, Code, INFRASTRUCTURE_ID AS InfrastructureId
                             FROM EQUIPMENTS
                             WHERE Code = :Code
                               AND (
                                   INFRASTRUCTURE_ID = :InfrastructureId
                                   OR (INFRASTRUCTURE_ID IS NULL AND :InfrastructureId IS NULL)
                               )";

        return await _connection.QuerySingleOrDefaultAsync<Equipment>(
            sql,
            new
            {
                Code = code.Trim(),
                InfrastructureId = infrastructureId?.ToString()
            });
    }

    public async Task<EquipmentDto?> GetDtoByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT e.Id AS {nameof(EquipmentDto.Id)},
                            e.Name AS {nameof(EquipmentDto.Name)},
                            e.Code AS {nameof(EquipmentDto.Code)},
                            e.EquipmentTypeId AS {nameof(EquipmentDto.EquipmentTypeId)},
                            e.INFRASTRUCTURE_ID AS {nameof(EquipmentDto.InfrastructureId)},
                            e.MANUFACTURE_YEAR AS {nameof(EquipmentDto.ManufactureYear)},
                            e.EQUIPMENT_STATUS_ID AS {nameof(EquipmentDto.EquipmentStatusId)},
                            e.IS_ACTIVE AS {nameof(EquipmentDto.IsActive)},
                            e.StatusTransition AS {nameof(EquipmentDto.StatusTransition)},
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
                            es.Name AS {nameof(EquipmentDto.EquipmentStatusName)},
                            eft.Name AS {nameof(EquipmentDto.FormTemplateName)},
                            eft.Id AS {nameof(EquipmentDto.FormTemplateId)},
                            eft.FormSchema AS {nameof(EquipmentDto.FormSchema)},
                            usr.Id AS CreatorId,
                            usr.UserName AS Username,
                            usr.FullName AS FullName
                     FROM EQUIPMENTS e
                     LEFT JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                     LEFT JOIN GridTypes gt ON et.GridTypeId = gt.Id
                     LEFT JOIN INFRASTRUCTURE inf ON e.INFRASTRUCTURE_ID = inf.Id
                     LEFT JOIN ORGANIZATION_UNIT u ON e.UnitId = u.Id
                     LEFT JOIN CATALOG es ON e.EQUIPMENT_STATUS_ID = es.Id
                     LEFT JOIN (
                           SELECT * FROM (
                               SELECT t.Id, v.Name, v.FormSchema, t.EquipmentTypeId,
                                      ROW_NUMBER() OVER (
                                          PARTITION BY t.EquipmentTypeId 
                                          ORDER BY CASE WHEN v.Status = 'Hoàn thành' THEN 0 ELSE 1 END, v.Version DESC
                                      ) as rn
                               FROM EavFormTemplates t
                               INNER JOIN EavFormTemplateVersions v ON t.Id = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0
                               WHERE t.IsDeleted = 0
                                 AND t.IsActive = 1
                                 AND t.FormType = 'TEMPLATE'
                           ) WHERE rn = 1
                       ) eft ON e.EquipmentTypeId = eft.EquipmentTypeId
                     LEFT JOIN APP_USER usr ON e.CreatorId = usr.Id
                     WHERE e.Id = :Id AND e.IsDeleted = 0";

        var result = await _connection.QueryAsync<EquipmentDto, CreatorInfoRow, EquipmentDto>(
            sql,
            (eq, creatorRow) =>
            {
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
                                MANUFACTURE_YEAR as {nameof(Equipment.ManufactureYear)},
                                EQUIPMENT_STATUS_ID as {nameof(Equipment.EquipmentStatusId)},
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
                                MANUFACTURE_YEAR as {nameof(Equipment.ManufactureYear)},
                                EQUIPMENT_STATUS_ID as {nameof(Equipment.EquipmentStatusId)},
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
        string? keyword,
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
                        LEFT JOIN CATALOG es ON e.EQUIPMENT_STATUS_ID = es.Id
                        LEFT JOIN APP_USER usr ON e.CreatorId = usr.Id
                        WHERE e.IsDeleted = 0";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(keyword))
        {
            sqlBase += " AND (LOWER(e.Code) LIKE :Keyword OR LOWER(e.Name) LIKE :Keyword)";
            parameters.Add("Keyword", $"%{keyword.ToLower().Trim()}%");
        }

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
                                   e.EquipmentTypeId AS {nameof(EquipmentDto.EquipmentTypeId)},
                                   e.INFRASTRUCTURE_ID AS {nameof(EquipmentDto.InfrastructureId)},
                                   e.MANUFACTURE_YEAR AS {nameof(EquipmentDto.ManufactureYear)},
                                   e.EQUIPMENT_STATUS_ID AS {nameof(EquipmentDto.EquipmentStatusId)},
                                   e.IS_ACTIVE AS {nameof(EquipmentDto.IsActive)},
                                   e.StatusTransition AS {nameof(EquipmentDto.StatusTransition)},
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
                                   es.Name AS {nameof(EquipmentDto.EquipmentStatusName)},
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
            (eq, creatorRow) =>
            {
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
                                           INFRASTRUCTURE_ID,
                                           MANUFACTURE_YEAR,
                                           EQUIPMENT_STATUS_ID,
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
                                           :InfrastructureId,
                                           :ManufactureYear,
                                           :EquipmentStatusId,
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
                InfrastructureId = equipment.InfrastructureId?.ToString(),
                equipment.ManufactureYear,
                equipment.EquipmentStatusId,
                IsActive = equipment.IsActive ? 1 : 0,
                CreatorId = equipment.CreatorId?.ToString(),
                equipment.CreatedBy,
                equipment.CreatedAt,
                equipment.UnitId,
                FormValues = OracleClob.Param(equipment.FormValues)
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
                    Value = OracleClob.Param(a.Value)
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
                        INFRASTRUCTURE_ID,
                        MANUFACTURE_YEAR,
                        EQUIPMENT_STATUS_ID,
                        IS_ACTIVE,
                        CreatorId,
                        CreatedBy,
                        CreatedAt,
                        IsDeleted,
                        UnitId,
StatusTransition,
                        FORM_VALUES
                    )
                    VALUES (
                        :Id,
                        :EquipmentTypeId,
                        :Name,
                        :Code,
                        :InfrastructureId,
                        :ManufactureYear,
                        :EquipmentStatusId,
                        :IsActive,
                        :CreatorId,
                        :CreatedBy,
                        :CreatedAt,
                        0,
                        :UnitId,
:StatusTransition,
                        :FormValues
                    )";

        var param = new
        {
            Id = equipment.Id.ToString(),
            EquipmentTypeId = equipment.EquipmentTypeId.ToString(),
            equipment.Name,
            equipment.Code,
            InfrastructureId = equipment.InfrastructureId?.ToString(),
            equipment.ManufactureYear,
            equipment.EquipmentStatusId,
            IsActive = equipment.IsActive ? 1 : 0,
            CreatorId = equipment.CreatorId?.ToString(),
            equipment.CreatedBy,
            equipment.CreatedAt,
            equipment.UnitId,
            StatusTransition = equipment.StatusTransition = null,
            FormValues = OracleClob.Param(equipment.FormValues)
        };

        var result = await _connection.ExecuteAsync(sql, param);
        return result > 0;
    }

    public async Task<bool> CloneForInfrastructureTransferAsync(
        Equipment sourceEquipment,
        Equipment replacementEquipment)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        using var transaction = _connection.BeginTransaction();

        try
        {
            const string insertEquipmentSql = @"INSERT INTO EQUIPMENTS (
                Id,
                EquipmentTypeId,
                Name,
                Code,
                SerialNumber,
                INFRASTRUCTURE_ID,
                MANUFACTURE_YEAR,
                EQUIPMENT_STATUS_ID,
                IS_ACTIVE,
                CreatorId,
                CreatedBy,
                CreatedAt,
                IsDeleted,
                UnitId,
                FORM_VALUES,
                Note,
                StatusTransition
            )
            VALUES (
                :Id,
                :EquipmentTypeId,
                :Name,
                :Code,
                :SerialNumber,
                :InfrastructureId,
                :ManufactureYear,
                :EquipmentStatusId,
                :IsActive,
                :CreatorId,
                :CreatedBy,
                :CreatedAt,
                0,
                :UnitId,
                :FormValues,
                :Note,
                NULL
            )";

            await _connection.ExecuteAsync(insertEquipmentSql, new
            {
                Id = replacementEquipment.Id.ToString(),
                EquipmentTypeId = replacementEquipment.EquipmentTypeId.ToString(),
                replacementEquipment.Name,
                replacementEquipment.Code,
                replacementEquipment.SerialNumber,
                InfrastructureId = replacementEquipment.InfrastructureId?.ToString(),
                replacementEquipment.ManufactureYear,
                replacementEquipment.EquipmentStatusId,
                IsActive = replacementEquipment.IsActive ? 1 : 0,
                CreatorId = replacementEquipment.CreatorId?.ToString(),
                replacementEquipment.CreatedBy,
                replacementEquipment.CreatedAt,
                replacementEquipment.UnitId,
                FormValues = OracleClob.Param(replacementEquipment.FormValues),
                replacementEquipment.Note,
            }, transaction);

            var sourceAttributes = await _connection.QueryAsync<AttributeValue>(
                "SELECT Id, EquipmentId, AttributeDefinitionId, Value FROM AttributeValues WHERE EquipmentId = :EquipmentId",
                new { EquipmentId = sourceEquipment.Id.ToString() },
                transaction);

            var copiedAttributes = sourceAttributes.Select(attribute => new
            {
                Id = Guid.NewGuid().ToString(),
                EquipmentId = replacementEquipment.Id.ToString(),
                AttributeDefinitionId = attribute.AttributeDefinitionId.ToString(),
                Value = OracleClob.Param(attribute.Value)
            });

            if (copiedAttributes.Any())
            {
                await _connection.ExecuteAsync(@"INSERT INTO AttributeValues (
                        Id,
                        EquipmentId,
                        AttributeDefinitionId,
                        Value
                    )
                    VALUES (
                        :Id,
                        :EquipmentId,
                        :AttributeDefinitionId,
                        :Value
                    )", copiedAttributes, transaction);
            }

            var sourceUpdated = await _connection.ExecuteAsync(@"UPDATE EQUIPMENTS
                SET IS_ACTIVE = 0,
                    ModifiedBy = :ModifiedBy,
                    ModifiedDate = :ModifiedDate,
                    StatusTransition = :StatusTransition
                WHERE Id = :Id
                  AND IsDeleted = 0",
                new
                {
                    Id = sourceEquipment.Id.ToString(),
                    sourceEquipment.ModifiedBy,
                    sourceEquipment.ModifiedDate,
                    sourceEquipment.StatusTransition
                },
                transaction);

            if (sourceUpdated != 1)
                throw new InvalidOperationException("Thiết bị nguồn không còn tồn tại hoặc đã bị xóa.");

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<Equipment?> GetDetailTransferTargetAsync(Equipment sourceEquipment)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT e.Id,
                   e.EquipmentTypeId,
                   e.Name,
                   e.Code,
                   e.SerialNumber,
                   e.INFRASTRUCTURE_ID AS InfrastructureId,
                   e.COUNTRY_ID AS CountryId,
                   e.MANUFACTURE_YEAR AS ManufactureYear,
                   e.EQUIPMENT_STATUS_ID AS EquipmentStatusId,
                   e.IS_ACTIVE AS IsActive,
                   e.StatusTransition AS StatusTransition,
                   e.CreatorId,
                   e.CreatedBy,
                   e.CreatedAt,
                   e.ModifiedBy,
                   e.ModifiedDate,
                   e.IsDeleted,
                   e.UnitId,
                   e.FORM_VALUES AS FormValues
            FROM EQUIPMENTS e
            WHERE e.Code = :Code
              AND e.Id <> :SourceEquipmentId
              AND e.IsDeleted = 0
              AND e.StatusTransition IS NULL
              AND (
                    e.INFRASTRUCTURE_ID <> :SourceInfrastructureId
                    OR (e.INFRASTRUCTURE_ID IS NULL AND :SourceInfrastructureId IS NOT NULL)
                    OR (e.INFRASTRUCTURE_ID IS NOT NULL AND :SourceInfrastructureId IS NULL)
                  )
            ORDER BY e.CreatedAt DESC, e.Id DESC
            FETCH FIRST 1 ROW ONLY";

        return await _connection.QuerySingleOrDefaultAsync<Equipment>(sql, new
        {
            sourceEquipment.Code,
            SourceEquipmentId = sourceEquipment.Id.ToString(),
            SourceInfrastructureId = sourceEquipment.InfrastructureId?.ToString()
        });
    }

    public async Task<bool> CloneDossiersAndDocumentsForDetailTransferAsync(
        Equipment sourceEquipment,
        Equipment replacementEquipment)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var copiedObjects = new List<(string ObjectKey, string VersionId)>();

        try
        {
            var dossierCopies = await PrepareDossierAndDocumentCopiesAsync(
                sourceEquipment,
                replacementEquipment,
                copiedObjects);

            using var transaction = _connection.BeginTransaction();

            try
            {
                await CloneDossiersAndDocumentsAsync(dossierCopies, replacementEquipment, transaction);

                var sourceUpdated = await _connection.ExecuteAsync(@"UPDATE EQUIPMENTS
                    SET ModifiedBy = :ModifiedBy,
                        ModifiedDate = :ModifiedDate,
                        StatusTransition = :StatusTransition
                    WHERE Id = :Id
                      AND IsDeleted = 0",
                    new
                    {
                        Id = sourceEquipment.Id.ToString(),
                        sourceEquipment.ModifiedBy,
                        sourceEquipment.ModifiedDate,
                        sourceEquipment.StatusTransition
                    },
                    transaction);

                if (sourceUpdated != 1)
                    throw new InvalidOperationException("Thiết bị nguồn không còn tồn tại hoặc đã bị xóa.");

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch
        {
            foreach (var copiedObject in copiedObjects)
            {
                try
                {
                    await _fileStorageService.DeleteFileAsync(
                        copiedObject.ObjectKey,
                        _fileStorageService.DossierBucketName,
                        copiedObject.VersionId);
                }
                catch
                {
                    // Preserve the original failure; a later cleanup job can remove any orphan object.
                }
            }

            throw;
        }
    }

    private async Task<List<DossierClone>> PrepareDossierAndDocumentCopiesAsync(
        Equipment sourceEquipment,
        Equipment replacementEquipment,
        ICollection<(string ObjectKey, string VersionId)> copiedObjects)
    {
        var dossiers = (await _connection.QueryAsync<TransferDossierRow>(@"
            SELECT d.ID AS Id,
                   d.DOSSIER_GROUP_ID AS DossierGroupId,
                   d.GridTypeId,
                   d.DossierSetId,
                   d.DossierTypeId,
                   d.FormDataJson,
                   d.STATUS_ID AS StatusId,
                   d.KIND_ID AS KindId,
                   d.PUBLISHSTATUSID AS PublishStatusId,
                   d.ShelfId,
                   d.FloorId,
                   d.BoxId
            FROM DOSSIERS d
            INNER JOIN DOSSIER_EQUIPMENTS de ON de.DossierId = d.ID
            WHERE de.EquipmentId = :EquipmentId
              AND d.IsDeleted = 0",
            new { EquipmentId = sourceEquipment.Id.ToString() })).ToList();

        var dossierCopies = dossiers.Select(dossier => new DossierClone
        {
            Source = dossier,
            Id = Guid.NewGuid()
        }).ToList();

        foreach (var dossierCopy in dossierCopies)
        {
            dossierCopy.Documents = (await _connection.QueryAsync<TransferDocumentRow>(@"
                SELECT ID AS Id,
                       NAME AS Name,
                       FOLDER_ID AS FolderId,
                       DOCUMENT_TYPE_ID AS DocumentTypeId,
                       STATUS AS Status
                FROM DOCUMENTS
                WHERE DOSSIER_ID = :DossierId
                  AND IS_DELETED = 0",
                new { DossierId = dossierCopy.Source.Id.ToString() }))
                .Select(document => new DocumentClone
                {
                    Source = document,
                    Id = Guid.NewGuid()
                })
                .ToList();

            foreach (var documentCopy in dossierCopy.Documents)
            {
                documentCopy.Versions = (await _connection.QueryAsync<TransferDocumentVersionRow>(@"
                    SELECT ID AS Id,
                           VERSION_NUMBER AS VersionNumber,
                           UPLOAD_SOURCE AS UploadSource,
                           FILE_PATH AS FilePath,
                           MINIO_VERSION_ID AS MinioVersionId,
                           FILE_SIZE AS FileSize,
                           MIME_TYPE AS MimeType,
                           PAGE_COUNT AS PageCount
                    FROM DOCUMENT_VERSIONS
                    WHERE DOCUMENT_ID = :DocumentId
                      AND IS_DELETED = 0
                    ORDER BY VERSION_NUMBER",
                    new { DocumentId = documentCopy.Source.Id.ToString() }))
                    .Select(version => new DocumentVersionClone
                    {
                        Source = version,
                        Id = Guid.NewGuid()
                    })
                    .ToList();
            }
        }

        var unitCode = await _connection.QuerySingleOrDefaultAsync<string>(
            "SELECT Code FROM ORGANIZATION_UNIT WHERE Id = :UnitId",
            new { UnitId = replacementEquipment.UnitId }) ?? "unknown";

        // Copy MinIO before opening the database transaction. Database failure is compensated by the caller.
        foreach (var dossierCopy in dossierCopies)
        {
            foreach (var documentCopy in dossierCopy.Documents)
            {
                foreach (var versionCopy in documentCopy.Versions)
                {
                    if (string.IsNullOrWhiteSpace(versionCopy.Source.FilePath))
                        continue;

                    var destinationFileName = GetDestinationFileName(
                        documentCopy.Source.Name,
                        versionCopy.Source.FilePath);
                    var destinationObjectKey = _fileStorageService.BuildDossierObjectKey(
                        unitCode,
                        dossierCopy.Id,
                        destinationFileName);
                    var copiedFile = await _fileStorageService.CopyFileWithVersionAsync(
                        versionCopy.Source.FilePath,
                        destinationObjectKey,
                        _fileStorageService.DossierBucketName,
                        _fileStorageService.DossierBucketName,
                        versionCopy.Source.MinioVersionId);

                    versionCopy.FilePath = copiedFile.ObjectKey;
                    versionCopy.MinioVersionId = copiedFile.VersionId;
                    copiedObjects.Add(copiedFile);
                }
            }
        }

        return dossierCopies;
    }

    private async Task CloneDossiersAndDocumentsAsync(
        IEnumerable<DossierClone> dossierCopies,
        Equipment replacementEquipment,
        IDbTransaction transaction)
    {
        foreach (var dossierCopy in dossierCopies)
        {
            await _connection.ExecuteAsync(@"INSERT INTO DOSSIERS (
                        Id,
                        DOSSIER_GROUP_ID,
                        GridTypeId,
                        InfrastructureId,
                        DossierSetId,
                        DossierTypeId,
                        FormDataJson,
                        STATUS_ID,
                        KIND_ID,
                        WorkflowInstanceId,
                        WorkflowStatusName,
                        RowVersion,
                        CreatorId,
                        CreatorUsername,
                        CreatorName,
                        CreatedBy,
                        CreatedDate,
                        IsDeleted,
                        PUBLISHSTATUSID,
                        ShelfId,
                        FloorId,
                        BoxId
                    ) VALUES (
                        :Id,
                        :DossierGroupId,
                        :GridTypeId,
                        :InfrastructureId,
                        :DossierSetId,
                        :DossierTypeId,
                        :FormDataJson,
                        :StatusId,
                        :KindId,
                        NULL,
                        NULL,
                        1,
                        :CreatorId,
                        :CreatorUsername,
                        :CreatorName,
                        :CreatedBy,
                        SYSTIMESTAMP,
                        0,
                        :PublishStatusId,
                        :ShelfId,
                        :FloorId,
                        :BoxId
                    )",
                new
                {
                    Id = dossierCopy.Id.ToString(),
                    dossierCopy.Source.DossierGroupId,
                    dossierCopy.Source.GridTypeId,
                    InfrastructureId = replacementEquipment.InfrastructureId?.ToString(),
                    DossierSetId = dossierCopy.Source.DossierSetId?.ToString(),
                    DossierTypeId = dossierCopy.Source.DossierTypeId.ToString(),
                    FormDataJson = OracleClob.Param(dossierCopy.Source.FormDataJson),
                    dossierCopy.Source.StatusId,
                    dossierCopy.Source.KindId,
                    CreatorId = replacementEquipment.CreatorId?.ToString(),
                    CreatorUsername = replacementEquipment.CreatedBy,
                    CreatorName = replacementEquipment.CreatedBy,
                    replacementEquipment.CreatedBy,
                    dossierCopy.Source.PublishStatusId,
                    dossierCopy.Source.ShelfId,
                    dossierCopy.Source.FloorId,
                    dossierCopy.Source.BoxId
                }, transaction);

            await _connection.ExecuteAsync(
                "INSERT INTO DOSSIER_EQUIPMENTS (DossierId, EquipmentId) VALUES (:DossierId, :EquipmentId)",
                new
                {
                    DossierId = dossierCopy.Id.ToString(),
                    EquipmentId = replacementEquipment.Id.ToString()
                },
                transaction);

            foreach (var documentCopy in dossierCopy.Documents)
            {
                await _connection.ExecuteAsync(@"INSERT INTO DOCUMENTS (
                            ID,
                            NAME,
                            FOLDER_ID,
                            DOSSIER_ID,
                            DOCUMENT_TYPE_ID,
                            STATUS,
                            ROW_VERSION,
                            CREATED_BY,
                            CREATOR_NAME,
                            CREATED_DATE,
                            IS_DELETED
                        ) VALUES (
                            :Id,
                            :Name,
                            :FolderId,
                            :DossierId,
                            :DocumentTypeId,
                            :Status,
                            1,
                            :CreatedBy,
                            :CreatorName,
                            SYSTIMESTAMP,
                            0
                        )",
                    new
                    {
                        Id = documentCopy.Id.ToString(),
                        documentCopy.Source.Name,
                        FolderId = documentCopy.Source.FolderId?.ToString(),
                        DossierId = dossierCopy.Id.ToString(),
                        DocumentTypeId = documentCopy.Source.DocumentTypeId?.ToString(),
                        documentCopy.Source.Status,
                        CreatedBy = replacementEquipment.CreatedBy,
                        CreatorName = replacementEquipment.CreatedBy
                    }, transaction);

                foreach (var versionCopy in documentCopy.Versions)
                {
                    await _connection.ExecuteAsync(@"INSERT INTO DOCUMENT_VERSIONS (
                                ID,
                                DOCUMENT_ID,
                                VERSION_NUMBER,
                                UPLOAD_SOURCE,
                                FILE_PATH,
                                MINIO_VERSION_ID,
                                FILE_SIZE,
                                MIME_TYPE,
                                PAGE_COUNT,
                                CREATED_BY,
                                CREATED_DATE,
                                IS_DELETED
                            ) VALUES (
                                :Id,
                                :DocumentId,
                                :VersionNumber,
                                :UploadSource,
                                :FilePath,
                                :MinioVersionId,
                                :FileSize,
                                :MimeType,
                                :PageCount,
                                :CreatedBy,
                                SYSTIMESTAMP,
                                0
                            )",
                        new
                        {
                            Id = versionCopy.Id.ToString(),
                            DocumentId = documentCopy.Id.ToString(),
                            versionCopy.Source.VersionNumber,
                            versionCopy.Source.UploadSource,
                            versionCopy.FilePath,
                            versionCopy.MinioVersionId,
                            versionCopy.Source.FileSize,
                            versionCopy.Source.MimeType,
                            versionCopy.Source.PageCount,
                            CreatedBy = replacementEquipment.CreatedBy
                        }, transaction);
                }
            }
        }
    }

    private static string GetDestinationFileName(string documentName, string sourceFilePath)
    {
        var extension = Path.GetExtension(sourceFilePath);
        return string.IsNullOrWhiteSpace(extension) || documentName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? documentName
            : $"{documentName}{extension}";
    }

    private sealed class TransferDossierRow
    {
        public Guid Id { get; init; }
        public int DossierGroupId { get; init; }
        public int? GridTypeId { get; init; }
        public Guid? DossierSetId { get; init; }
        public Guid DossierTypeId { get; init; }
        public string? FormDataJson { get; init; }
        public int StatusId { get; init; }
        public int KindId { get; init; }
        public int? PublishStatusId { get; init; }
        public long? ShelfId { get; init; }
        public long? FloorId { get; init; }
        public long? BoxId { get; init; }
    }

    private sealed class TransferDocumentRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public Guid? FolderId { get; init; }
        public Guid? DocumentTypeId { get; init; }
        public string Status { get; init; } = "Active";
    }

    private sealed class TransferDocumentVersionRow
    {
        public Guid Id { get; init; }
        public int VersionNumber { get; init; }
        public int UploadSource { get; init; }
        public string? FilePath { get; init; }
        public string? MinioVersionId { get; init; }
        public long FileSize { get; init; }
        public string? MimeType { get; init; }
        public int PageCount { get; init; }
    }

    private sealed class DossierClone
    {
        public required TransferDossierRow Source { get; init; }
        public Guid Id { get; init; }
        public List<DocumentClone> Documents { get; set; } = [];
    }

    private sealed class DocumentClone
    {
        public required TransferDocumentRow Source { get; init; }
        public Guid Id { get; init; }
        public List<DocumentVersionClone> Versions { get; set; } = [];
    }

    private sealed class DocumentVersionClone
    {
        public required TransferDocumentVersionRow Source { get; init; }
        public Guid Id { get; init; }
        public string? FilePath { get; set; }
        public string? MinioVersionId { get; set; }
    }

    public async Task<bool> UpdateAsync(Equipment equipment)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"UPDATE EQUIPMENTS
                    SET EquipmentTypeId = :EquipmentTypeId,
                        Name = :Name,
                        Code = :Code,
                        INFRASTRUCTURE_ID = :InfrastructureId,
                        MANUFACTURE_YEAR = :ManufactureYear,
                        EQUIPMENT_STATUS_ID = :EquipmentStatusId,
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
            InfrastructureId = equipment.InfrastructureId?.ToString(),
            equipment.ManufactureYear,
            equipment.EquipmentStatusId,
            IsActive = equipment.IsActive ? 1 : 0,
            equipment.ModifiedBy,
            ModifiedDate = DateTime.UtcNow,
            equipment.UnitId,
            FormValues = OracleClob.Param(equipment.FormValues)
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
                    Value = OracleClob.Param(a.Value)
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
    public async Task<IEnumerable<OrganizationDto>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        if (startUnitId.HasValue)
        {
            var sql = @"SELECT Id, Code, Name, ParentId, IsActive, IsDeleted
                        FROM ORGANIZATION_UNIT
                        WHERE IsActive = 1 AND IsDeleted = 0
                        START WITH Id = :StartUnitId
                        CONNECT BY PRIOR Id = ParentId";
            return await _connection.QueryAsync<OrganizationDto>(sql, new { StartUnitId = startUnitId.Value });
        }
        else
        {
            var sql = @"SELECT Id, Code, Name, ParentId, IsActive, IsDeleted
                        FROM ORGANIZATION_UNIT
                        WHERE IsActive = 1 AND IsDeleted = 0
                        ORDER BY Name ASC";
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

    public async Task<int> CountByInfrastructureIdAsync(Guid infrastructureId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"SELECT COUNT(1)
                             FROM EQUIPMENTS
                             WHERE INFRASTRUCTURE_ID = :InfrastructureId
                               AND IsDeleted = 0";

        return await _connection.ExecuteScalarAsync<int>(sql, new { InfrastructureId = infrastructureId.ToString() });
    }
}
