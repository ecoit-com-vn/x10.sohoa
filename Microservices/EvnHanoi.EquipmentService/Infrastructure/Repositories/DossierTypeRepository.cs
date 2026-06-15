using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DossierTypeRepository : IDossierTypeRepository
{
    private readonly IDbConnection _connection;

    public DossierTypeRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<DossierType?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT dt.{nameof(DossierType.Id)},
                            dt.{nameof(DossierType.Name)},
                            dt.{nameof(DossierType.Code)},
                            dt.FORM_ID as {nameof(DossierType.FormId)},
                            dt.IS_ACTIVE as {nameof(DossierType.IsActive)},
                            dt.PIORITY as {nameof(DossierType.Piority)},
                            dt.{nameof(DossierType.CreatedBy)},
                            dt.{nameof(DossierType.CreatedDate)},
                            dt.{nameof(DossierType.ModifiedBy)},
                            dt.{nameof(DossierType.ModifiedDate)},
                            dt.{nameof(DossierType.IsDeleted)},
                            f.Name as {nameof(DossierType.FormName)}
                     FROM DOSSIER_TYPES dt
                     LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
                     WHERE dt.{nameof(DossierType.Id)} = :Id AND dt.{nameof(DossierType.IsDeleted)} = 0";

        return await _connection.QuerySingleOrDefaultAsync<DossierType>(sql, new { Id = id.ToString() });
    }

    public async Task<DossierType?> GetByCodeAsync(string code)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"SELECT dt.{nameof(DossierType.Id)},
                            dt.{nameof(DossierType.Name)},
                            dt.{nameof(DossierType.Code)},
                            dt.FORM_ID as {nameof(DossierType.FormId)},
                            dt.IS_ACTIVE as {nameof(DossierType.IsActive)},
                            dt.PIORITY as {nameof(DossierType.Piority)},
                            dt.{nameof(DossierType.CreatedBy)},
                            dt.{nameof(DossierType.CreatedDate)},
                            dt.{nameof(DossierType.ModifiedBy)},
                            dt.{nameof(DossierType.ModifiedDate)},
                            dt.{nameof(DossierType.IsDeleted)},
                            f.Name as {nameof(DossierType.FormName)}
                     FROM DOSSIER_TYPES dt
                     LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
                     WHERE LOWER(dt.{nameof(DossierType.Code)}) = :Code AND dt.{nameof(DossierType.IsDeleted)} = 0";

        return await _connection.QuerySingleOrDefaultAsync<DossierType>(sql, new { Code = code.ToLower().Trim() });
    }

    public async Task<(IEnumerable<DossierType> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        string? keyword, 
        int? status)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sqlBase = $@"FROM DOSSIER_TYPES dt
                         LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
                         WHERE dt.{nameof(DossierType.IsDeleted)} = 0";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(keyword))
        {
            sqlBase += $" AND (LOWER(dt.{nameof(DossierType.Code)}) LIKE :Keyword OR LOWER(dt.{nameof(DossierType.Name)}) LIKE :Keyword)";
            parameters.Add("Keyword", $"%{keyword.ToLower().Trim()}%");
        }

        if (status.HasValue)
        {
            sqlBase += $" AND dt.IS_ACTIVE = :Status";
            parameters.Add("Status", status.Value);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectSql = $@"SELECT dt.{nameof(DossierType.Id)},
                                   dt.{nameof(DossierType.Name)},
                                   dt.{nameof(DossierType.Code)},
                                   dt.FORM_ID as {nameof(DossierType.FormId)},
                                   dt.IS_ACTIVE as {nameof(DossierType.IsActive)},
                                   dt.PIORITY as {nameof(DossierType.Piority)},
                                   dt.{nameof(DossierType.CreatedBy)},
                                   dt.{nameof(DossierType.CreatedDate)},
                                   dt.{nameof(DossierType.ModifiedBy)},
                                   dt.{nameof(DossierType.ModifiedDate)},
                                   dt.{nameof(DossierType.IsDeleted)},
                                   f.Name as {nameof(DossierType.FormName)}
                           {sqlBase}
                           ORDER BY dt.PIORITY ASC, dt.{nameof(DossierType.CreatedDate)} DESC
                           OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var items = await _connection.QueryAsync<DossierType>(selectSql, parameters);

        return (items, totalCount);
    }

    public async Task<Guid> CreateAsync(DossierType dossierType)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        if (dossierType.Id == Guid.Empty)
        {
            dossierType.Id = Guid.Parse(EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid());
        }

        var sql = $@"INSERT INTO DOSSIER_TYPES (
                        {nameof(DossierType.Id)},
                        {nameof(DossierType.Name)},
                        {nameof(DossierType.Code)},
                        FORM_ID,
                        IS_ACTIVE,
                        PIORITY,
                        {nameof(DossierType.CreatedBy)},
                        {nameof(DossierType.CreatedDate)},
                        {nameof(DossierType.IsDeleted)}
                    )
                    VALUES (:Id, :Name, :Code, :FormId, :IsActive, :Piority, :CreatedBy, :CreatedDate, :IsDeleted)";

        var param = new
        {
            Id = dossierType.Id.ToString(),
            dossierType.Name,
            dossierType.Code,
            FormId = dossierType.FormId?.ToString(),
            IsActive = dossierType.IsActive ? 1 : 0,
            dossierType.Piority,
            dossierType.CreatedBy,
            dossierType.CreatedDate,
            IsDeleted = dossierType.IsDeleted ? 1 : 0
        };

        await _connection.ExecuteAsync(sql, param);
        return dossierType.Id;
    }

    public async Task<bool> UpdateAsync(DossierType dossierType)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"UPDATE DOSSIER_TYPES
                    SET {nameof(DossierType.Name)} = :Name,
                        {nameof(DossierType.Code)} = :Code,
                        FORM_ID = :FormId,
                        IS_ACTIVE = :IsActive,
                        PIORITY = :Piority,
                        {nameof(DossierType.ModifiedBy)} = :ModifiedBy,
                        {nameof(DossierType.ModifiedDate)} = :ModifiedDate
                    WHERE {nameof(DossierType.Id)} = :Id AND {nameof(DossierType.IsDeleted)} = 0";

        var param = new
        {
            Id = dossierType.Id.ToString(),
            dossierType.Name,
            dossierType.Code,
            FormId = dossierType.FormId?.ToString(),
            IsActive = dossierType.IsActive ? 1 : 0,
            dossierType.Piority,
            dossierType.ModifiedBy,
            dossierType.ModifiedDate
        };

        var affected = await _connection.ExecuteAsync(sql, param);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        var sql = $@"UPDATE DOSSIER_TYPES
                    SET {nameof(DossierType.IsDeleted)} = 1
                    WHERE {nameof(DossierType.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, new { Id = id.ToString() });
        return affected > 0;
    }
}
