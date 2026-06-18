using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;
using GridTypeEntity = EvnHanoi.EquipmentService.Core.Entities.GridType;
namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DossierRepository : IDossierRepository
{
    private readonly IDbConnection _connection;
    public DossierRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"SELECT ID, CODE, NAME, INFRA_TYPE_ID as InfraTypeId, UNIT_ID as UnitId, IS_ACTIVE as IsActive 
                    FROM INFRASTRUCTURE 
                    WHERE IsDeleted = 0 
                    ORDER BY NAME ASC";
        return await _connection.QueryAsync<InfrastructureEntity>(sql);
    }

    public async Task<IEnumerable<GridTypeEntity>> GetGridTypesLookupAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = "SELECT Id, Name FROM GridTypes ORDER BY Id ASC";
        return await _connection.QueryAsync<GridTypeEntity>(sql);
    }
    public async Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"SELECT Id,
                           Name,
                           Code,
                           FORM_ID  AS FormId,
                           IS_ACTIVE AS IsActive,
                           PIORITY   AS Piority
                    FROM DOSSIER_TYPES
                    WHERE IsDeleted = 0
                      AND IS_ACTIVE = 1
                    ORDER BY PIORITY ASC, Id ASC";
        return await _connection.QueryAsync<DossierType>(sql);
    }


    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var parameters = new DynamicParameters();
        var sqlBase = $@"FROM DOSSIERS d
                         LEFT JOIN INFRASTRUCTURE i ON d.{nameof(Dossier.InfrastructureId)} = i.ID
                         LEFT JOIN DOSSIER_TYPES dt ON d.{nameof(Dossier.DossierTypeId)} = dt.ID
                         LEFT JOIN DOSSIER_SETS ds ON d.{nameof(Dossier.DossierSetId)} = ds.ID
                         WHERE d.{nameof(Dossier.IsDeleted)} = 0";
        // Filter theo keyword (tìm trong FormDataJson)
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            sqlBase += $" AND UPPER(d.{nameof(Dossier.FormDataJson)}) LIKE :Keyword";
            parameters.Add("Keyword", $"%{filter.Keyword.ToUpper().Trim()}%");
        }
        // Filter theo trạm
        if (filter.InfrastructureId.HasValue)
        {
            sqlBase += $" AND d.{nameof(Dossier.InfrastructureId)} = :InfrastructureId";
            parameters.Add("InfrastructureId", filter.InfrastructureId.Value.ToString());
        }
        // Filter theo loại lưới điện
        if (filter.GridTypeId.HasValue)
        {
            sqlBase += $" AND d.{nameof(Dossier.GridTypeId)} = :GridTypeId";
            parameters.Add("GridTypeId", filter.GridTypeId.Value);
        }
        // Filter theo unit (bao gồm unit con — sử dụng CONNECT BY nếu Oracle, hoặc chỉ đơn giản là UnitId)
        // Note: Đây là filter cơ bản; nếu cần phân cấp unit thì mở rộng thêm subquery sau
        if (filter.UnitId.HasValue)
        {
            sqlBase += " AND i.UNIT_ID = :UnitId";
            parameters.Add("UnitId", filter.UnitId.Value);
        }
        // Filter theo trạng thái
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            sqlBase += $" AND d.{nameof(Dossier.Status)} = :Status";
            parameters.Add("Status", filter.Status);
        }
        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
        var selectSql = $@"SELECT
                            d.{nameof(Dossier.Id)},
                            d.{nameof(Dossier.GridTypeId)},
                            d.{nameof(Dossier.InfrastructureId)},
                            i.NAME as {nameof(DossierListItemDto.InfrastructureName)},
                            i.CODE as {nameof(DossierListItemDto.InfrastructureCode)},
                            d.{nameof(Dossier.DossierSetId)},
                            ds.NAME as {nameof(DossierListItemDto.DossierSetName)},
                            d.{nameof(Dossier.DossierTypeId)},
                            dt.NAME as {nameof(DossierListItemDto.DossierTypeName)},
                            d.{nameof(Dossier.Status)},
                            d.{nameof(Dossier.WorkflowStatusName)},
                            d.{nameof(Dossier.CreatorName)},
                            d.{nameof(Dossier.CreatedDate)},
                            0 as {nameof(DossierListItemDto.DocumentCount)}
                         {sqlBase}
                         ORDER BY d.{nameof(Dossier.CreatedDate)} DESC
                         OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";
        parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);
        parameters.Add("PageSize", filter.PageSize);
        var items = await _connection.QueryAsync<DossierListItemDto>(selectSql, parameters);
        return (items, totalCount);
    }
    public async Task<DossierDetailDto?> GetDetailByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"SELECT
                        d.{nameof(Dossier.Id)},
                        d.{nameof(Dossier.GridTypeId)},
                        d.{nameof(Dossier.InfrastructureId)},
                        i.NAME as {nameof(DossierDetailDto.InfrastructureName)},
                        i.CODE as {nameof(DossierDetailDto.InfrastructureCode)},
                        d.{nameof(Dossier.DossierSetId)},
                        ds.NAME as {nameof(DossierDetailDto.DossierSetName)},
                        d.{nameof(Dossier.DossierTypeId)},
                        dt.NAME as {nameof(DossierDetailDto.DossierTypeName)},
                        d.{nameof(Dossier.FormDataJson)},
                        d.{nameof(Dossier.Status)},
                        d.{nameof(Dossier.WorkflowInstanceId)},
                        d.{nameof(Dossier.WorkflowStatusName)},
                        d.{nameof(Dossier.RowVersion)},
                        d.{nameof(Dossier.CreatorId)},
                        d.{nameof(Dossier.CreatorUsername)},
                        d.{nameof(Dossier.CreatorName)},
                        d.{nameof(Dossier.CreatedBy)},
                        d.{nameof(Dossier.CreatedDate)},
                        d.{nameof(Dossier.ModifiedBy)},
                        d.{nameof(Dossier.ModifiedDate)}
                     FROM DOSSIERS d
                     LEFT JOIN INFRASTRUCTURE i ON d.{nameof(Dossier.InfrastructureId)} = i.ID
                     LEFT JOIN DOSSIER_TYPES dt ON d.{nameof(Dossier.DossierTypeId)} = dt.ID
                     LEFT JOIN DOSSIER_SETS ds ON d.{nameof(Dossier.DossierSetId)} = ds.ID
                     WHERE d.{nameof(Dossier.Id)} = :Id AND d.{nameof(Dossier.IsDeleted)} = 0";
        var dossier = await _connection.QuerySingleOrDefaultAsync<DossierDetailDto>(sql, new { Id = id.ToString() });
        if (dossier == null) return null;
        // Populate Creator info
        if (dossier.Creator == null)
        {
            // Creator info is embedded in the dossier columns
        }
        // Get equipment list
        dossier.Equipments = (await GetEquipmentsAsync(id)).ToList();
        return dossier;
    }
    public async Task<Dossier?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"SELECT * FROM DOSSIERS WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<Dossier>(sql, new { Id = id.ToString() });
    }
    public async Task<Guid> CreateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        if (dossier.Id == Guid.Empty)
            dossier.Id = Guid.Parse(UuidHelper.NewUuid());
        using var transaction = _connection.BeginTransaction();
        try
        {
            var sql = $@"INSERT INTO DOSSIERS (
                            {nameof(Dossier.Id)},
                            {nameof(Dossier.GridTypeId)},
                            {nameof(Dossier.InfrastructureId)},
                            {nameof(Dossier.DossierSetId)},
                            {nameof(Dossier.DossierTypeId)},
                            {nameof(Dossier.FormDataJson)},
                            {nameof(Dossier.Status)},
                            {nameof(Dossier.RowVersion)},
                            {nameof(Dossier.CreatorId)},
                            {nameof(Dossier.CreatorUsername)},
                            {nameof(Dossier.CreatorName)},
                            {nameof(Dossier.CreatedBy)},
                            {nameof(Dossier.CreatedDate)},
                            {nameof(Dossier.IsDeleted)}
                        ) VALUES (
                            :Id, :GridTypeId, :InfrastructureId, :DossierSetId, :DossierTypeId,
                            :FormDataJson, :Status, :RowVersion, :CreatorId, :CreatorUsername,
                            :CreatorName, :CreatedBy, :CreatedDate, :IsDeleted
                        )";
            await _connection.ExecuteAsync(sql, new
            {
                Id = dossier.Id.ToString(),
                dossier.GridTypeId,
                InfrastructureId = dossier.InfrastructureId?.ToString(),
                DossierSetId = dossier.DossierSetId?.ToString(),
                DossierTypeId = dossier.DossierTypeId.ToString(),
                dossier.FormDataJson,
                dossier.Status,
                dossier.RowVersion,
                CreatorId = dossier.CreatorId?.ToString(),
                dossier.CreatorUsername,
                dossier.CreatorName,
                dossier.CreatedBy,
                dossier.CreatedDate,
                IsDeleted = dossier.IsDeleted ? 1 : 0
            }, transaction);
            // Insert equipment links
            foreach (var equipId in equipmentIds)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO DOSSIER_EQUIPMENTS (DossierId, EquipmentId) VALUES (:DossierId, :EquipmentId)",
                    new { DossierId = dossier.Id.ToString(), EquipmentId = equipId.ToString() },
                    transaction);
            }
            transaction.Commit();
            return dossier.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public async Task<bool> UpdateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var sql = $@"UPDATE DOSSIERS SET
                            {nameof(Dossier.GridTypeId)} = :GridTypeId,
                            {nameof(Dossier.InfrastructureId)} = :InfrastructureId,
                            {nameof(Dossier.DossierSetId)} = :DossierSetId,
                            {nameof(Dossier.DossierTypeId)} = :DossierTypeId,
                            {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                            {nameof(Dossier.ModifiedDate)} = :ModifiedDate,
                            {nameof(Dossier.RowVersion)} = {nameof(Dossier.RowVersion)} + 1
                         WHERE {nameof(Dossier.Id)} = :Id
                           AND {nameof(Dossier.RowVersion)} = :RowVersion
                           AND {nameof(Dossier.IsDeleted)} = 0";
            var affected = await _connection.ExecuteAsync(sql, new
            {
                Id = dossier.Id.ToString(),
                dossier.GridTypeId,
                InfrastructureId = dossier.InfrastructureId?.ToString(),
                DossierSetId = dossier.DossierSetId?.ToString(),
                DossierTypeId = dossier.DossierTypeId.ToString(),
                dossier.ModifiedBy,
                dossier.ModifiedDate,
                dossier.RowVersion
            }, transaction);
            if (affected == 0)
            {
                transaction.Rollback();
                throw new Exception("Concurrency conflict: Hồ sơ đã được cập nhật bởi người dùng khác.");
            }
            // Update equipment list: xóa cũ, thêm mới
            await _connection.ExecuteAsync(
                "DELETE FROM DOSSIER_EQUIPMENTS WHERE DossierId = :DossierId",
                new { DossierId = dossier.Id.ToString() }, transaction);
            foreach (var equipId in equipmentIds)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO DOSSIER_EQUIPMENTS (DossierId, EquipmentId) VALUES (:DossierId, :EquipmentId)",
                    new { DossierId = dossier.Id.ToString(), EquipmentId = equipId.ToString() },
                    transaction);
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
    public async Task<bool> SoftDeleteAsync(Guid id, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.IsDeleted)} = 1,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate
                     WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }
    public async Task<bool> UpdateWorkflowAsync(Guid id, Guid workflowInstanceId, string workflowStatusName, string status, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.WorkflowInstanceId)} = :WorkflowInstanceId,
                        {nameof(Dossier.WorkflowStatusName)} = :WorkflowStatusName,
                        {nameof(Dossier.Status)} = :Status,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate
                     WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            WorkflowInstanceId = workflowInstanceId.ToString(),
            WorkflowStatusName = workflowStatusName,
            Status = status,
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }
    public async Task<bool> UpdateFormDataAsync(Guid id, string formDataJson, int expectedRowVersion, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.FormDataJson)} = :FormDataJson,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate,
                        {nameof(Dossier.RowVersion)} = {nameof(Dossier.RowVersion)} + 1
                     WHERE {nameof(Dossier.Id)} = :Id
                       AND {nameof(Dossier.RowVersion)} = :ExpectedRowVersion
                       AND {nameof(Dossier.IsDeleted)} = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            FormDataJson = formDataJson,
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow,
            ExpectedRowVersion = expectedRowVersion
        });
        if (affected == 0)
            throw new Exception("Concurrency conflict: Hồ sơ đã được cập nhật bởi người dùng khác.");
        return true;
    }
    public async Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"SELECT
                        de.EquipmentId,
                        e.CODE as EquipmentCode,
                        e.NAME as EquipmentName,
                        e.SerialNumber,
                        et.NAME as EquipmentTypeName,
                        i.NAME as InfrastructureName
                     FROM DOSSIER_EQUIPMENTS de
                     INNER JOIN Equipments e ON de.EquipmentId = e.Id
                     LEFT JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                     LEFT JOIN INFRASTRUCTURE i ON e.Infrastructure_Id = i.ID
                     WHERE de.DossierId = :DossierId";
        return await _connection.QueryAsync<DossierEquipmentDto>(sql, new { DossierId = dossierId.ToString() });
    }
    public async Task<bool> AddEquipmentAsync(Guid dossierId, Guid equipmentId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        // Check không trùng
        var exists = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM DOSSIER_EQUIPMENTS WHERE DossierId = :DossierId AND EquipmentId = :EquipmentId",
            new { DossierId = dossierId.ToString(), EquipmentId = equipmentId.ToString() });
        if (exists > 0) return true;
        var affected = await _connection.ExecuteAsync(
            "INSERT INTO DOSSIER_EQUIPMENTS (DossierId, EquipmentId) VALUES (:DossierId, :EquipmentId)",
            new { DossierId = dossierId.ToString(), EquipmentId = equipmentId.ToString() });
        return affected > 0;
    }
    public async Task<bool> RemoveEquipmentAsync(Guid dossierId, Guid equipmentId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var affected = await _connection.ExecuteAsync(
            "DELETE FROM DOSSIER_EQUIPMENTS WHERE DossierId = :DossierId AND EquipmentId = :EquipmentId",
            new { DossierId = dossierId.ToString(), EquipmentId = equipmentId.ToString() });
        return affected > 0;
    }
    public async Task<int> CreateVersionAsync(DossierVersion version)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        // Lấy version number tiếp theo
        var maxVersion = await _connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(VersionNumber), 0) FROM DOSSIER_VERSIONS WHERE DossierId = :DossierId",
            new { DossierId = version.DossierId.ToString() });
        version.VersionNumber = maxVersion + 1;
        version.Id = Guid.Parse(UuidHelper.NewUuid());
        var sql = $@"INSERT INTO DOSSIER_VERSIONS (
                        {nameof(DossierVersion.Id)},
                        {nameof(DossierVersion.DossierId)},
                        {nameof(DossierVersion.VersionNumber)},
                        {nameof(DossierVersion.FormDataJson)},
                        {nameof(DossierVersion.ChangeNote)},
                        {nameof(DossierVersion.CreatedBy)},
                        {nameof(DossierVersion.CreatedDate)}
                    ) VALUES (:Id, :DossierId, :VersionNumber, :FormDataJson, :ChangeNote, :CreatedBy, :CreatedDate)";
        await _connection.ExecuteAsync(sql, new
        {
            Id = version.Id.ToString(),
            DossierId = version.DossierId.ToString(),
            version.VersionNumber,
            version.FormDataJson,
            version.ChangeNote,
            version.CreatedBy,
            version.CreatedDate
        });
        return version.VersionNumber;
    }
    public async Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"SELECT
                        {nameof(DossierVersion.Id)},
                        {nameof(DossierVersion.DossierId)},
                        {nameof(DossierVersion.VersionNumber)},
                        {nameof(DossierVersion.FormDataJson)},
                        {nameof(DossierVersion.ChangeNote)},
                        {nameof(DossierVersion.CreatedBy)},
                        {nameof(DossierVersion.CreatedDate)}
                     FROM DOSSIER_VERSIONS
                     WHERE {nameof(DossierVersion.DossierId)} = :DossierId
                     ORDER BY {nameof(DossierVersion.VersionNumber)} DESC";
        return await _connection.QueryAsync<DossierVersionDto>(sql, new { DossierId = dossierId.ToString() });
    }
}