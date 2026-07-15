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

    private async Task PopulateDocumentTypeIdsAsync(DossierType dossierType)
    {
        if (dossierType == null) return;
        var sql = "SELECT DOCUMENT_TYPE_ID FROM DOSSIER_TYPE_DOCUMENT_TYPES WHERE DOSSIER_TYPE_ID = :DossierTypeId";
        var ids = await _connection.QueryAsync<string>(sql, new { DossierTypeId = dossierType.Id.ToString() });
        dossierType.DocumentTypeIds = ids.Select(Guid.Parse).ToList();
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

        var dossierType = await _connection.QuerySingleOrDefaultAsync<DossierType>(sql, new { Id = id.ToString() });
        if (dossierType != null)
        {
            await PopulateDocumentTypeIdsAsync(dossierType);
            var namesSql = @"SELECT LISTAGG(doc.NAME, ', ') WITHIN GROUP (ORDER BY doc.NAME)
                             FROM DOSSIER_TYPE_DOCUMENT_TYPES l
                             JOIN DOCUMENT_TYPES doc ON l.DOCUMENT_TYPE_ID = doc.ID
                             WHERE l.DOSSIER_TYPE_ID = :DossierTypeId AND doc.IsDeleted = 0";
            dossierType.DocumentTypeNames = await _connection.QuerySingleOrDefaultAsync<string>(
                namesSql,
                new { DossierTypeId = dossierType.Id.ToString() });
        }
        return dossierType;
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

        var dossierType = await _connection.QuerySingleOrDefaultAsync<DossierType>(sql, new { Code = code.ToLower().Trim() });
        if (dossierType != null)
        {
            await PopulateDocumentTypeIdsAsync(dossierType);
        }
        return dossierType;
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
                                   f.Name as {nameof(DossierType.FormName)},
                                   (SELECT LISTAGG(doc.NAME, ', ') WITHIN GROUP (ORDER BY doc.NAME) 
                                    FROM DOSSIER_TYPE_DOCUMENT_TYPES l
                                    JOIN DOCUMENT_TYPES doc ON l.DOCUMENT_TYPE_ID = doc.ID
                                    WHERE l.DOSSIER_TYPE_ID = dt.ID AND doc.IsDeleted = 0) as {nameof(DossierType.DocumentTypeNames)}
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

        using var transaction = _connection.BeginTransaction();
        try
        {
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

            await _connection.ExecuteAsync(sql, param, transaction);

            if (dossierType.DocumentTypeIds != null && dossierType.DocumentTypeIds.Any())
            {
                var sqlLink = "INSERT INTO DOSSIER_TYPE_DOCUMENT_TYPES (DOSSIER_TYPE_ID, DOCUMENT_TYPE_ID) VALUES (:DossierTypeId, :DocumentTypeId)";
                var linkParams = dossierType.DocumentTypeIds.Select(docId => new
                {
                    DossierTypeId = dossierType.Id.ToString(),
                    DocumentTypeId = docId.ToString()
                });
                await _connection.ExecuteAsync(sqlLink, linkParams, transaction);
            }

            transaction.Commit();
            return dossierType.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateAsync(DossierType dossierType)
    {
        if (_connection.State != ConnectionState.Open) 
            _connection.Open();

        using var transaction = _connection.BeginTransaction();
        try
        {
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

            var affected = await _connection.ExecuteAsync(sql, param, transaction);
            if (affected <= 0)
            {
                transaction.Rollback();
                return false;
            }

            // Update links
            var sqlDeleteLink = "DELETE FROM DOSSIER_TYPE_DOCUMENT_TYPES WHERE DOSSIER_TYPE_ID = :DossierTypeId";
            await _connection.ExecuteAsync(sqlDeleteLink, new { DossierTypeId = dossierType.Id.ToString() }, transaction);

            if (dossierType.DocumentTypeIds != null && dossierType.DocumentTypeIds.Any())
            {
                var sqlLink = "INSERT INTO DOSSIER_TYPE_DOCUMENT_TYPES (DOSSIER_TYPE_ID, DOCUMENT_TYPE_ID) VALUES (:DossierTypeId, :DocumentTypeId)";
                var linkParams = dossierType.DocumentTypeIds.Select(docId => new
                {
                    DossierTypeId = dossierType.Id.ToString(),
                    DocumentTypeId = docId.ToString()
                });
                await _connection.ExecuteAsync(sqlLink, linkParams, transaction);
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

        var sql = $@"UPDATE DOSSIER_TYPES
                    SET {nameof(DossierType.IsDeleted)} = 1
                    WHERE {nameof(DossierType.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, new { Id = id.ToString() });
        return affected > 0;
    }
}
