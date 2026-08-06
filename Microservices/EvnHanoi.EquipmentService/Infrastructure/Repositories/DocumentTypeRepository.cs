using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DocumentTypeRepository : IDocumentTypeRepository
{
    private readonly IDbConnection _connection;

    public DocumentTypeRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<DocumentType?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT dt.{nameof(DocumentType.Id)},
                            dt.{nameof(DocumentType.Name)},
                            dt.{nameof(DocumentType.Code)},
                            dt.FORM_ID as {nameof(DocumentType.FormId)},
                             dt.IS_ACTIVE as {nameof(DocumentType.IsActive)},
                             dt.IS_EQUIPMENT_PROFILE as {nameof(DocumentType.IsEquipmentProfile)},
                             dt.IS_FACTORY_ACCEPTANCE_REPORT as {nameof(DocumentType.IsFactoryAcceptanceReport)},
                             dt.IS_CBM_DOCUMENT as {nameof(DocumentType.IsCbmDocument)},
                             dt.PIORITY as {nameof(DocumentType.Piority)},
                            dt.{nameof(DocumentType.CreatedBy)},
                            dt.{nameof(DocumentType.CreatedDate)},
                            dt.{nameof(DocumentType.ModifiedBy)},
                            dt.{nameof(DocumentType.ModifiedDate)},
                            dt.{nameof(DocumentType.IsDeleted)},
                            f.Name as {nameof(DocumentType.FormName)}
                     FROM DOCUMENT_TYPES dt
                     LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
                     WHERE dt.{nameof(DocumentType.Id)} = :Id AND dt.{nameof(DocumentType.IsDeleted)} = 0";

        return await _connection.QuerySingleOrDefaultAsync<DocumentType>(sql, new { Id = id.ToString() });
    }

    public async Task<DocumentType?> GetByCodeAsync(string code)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT dt.{nameof(DocumentType.Id)},
                            dt.{nameof(DocumentType.Name)},
                            dt.{nameof(DocumentType.Code)},
                            dt.FORM_ID as {nameof(DocumentType.FormId)},
                             dt.IS_ACTIVE as {nameof(DocumentType.IsActive)},
                             dt.IS_EQUIPMENT_PROFILE as {nameof(DocumentType.IsEquipmentProfile)},
                             dt.IS_FACTORY_ACCEPTANCE_REPORT as {nameof(DocumentType.IsFactoryAcceptanceReport)},
                             dt.IS_CBM_DOCUMENT as {nameof(DocumentType.IsCbmDocument)},
                             dt.PIORITY as {nameof(DocumentType.Piority)},
                            dt.{nameof(DocumentType.CreatedBy)},
                            dt.{nameof(DocumentType.CreatedDate)},
                            dt.{nameof(DocumentType.ModifiedBy)},
                            dt.{nameof(DocumentType.ModifiedDate)},
                            dt.{nameof(DocumentType.IsDeleted)},
                            f.Name as {nameof(DocumentType.FormName)}
                     FROM DOCUMENT_TYPES dt
                     LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
                     WHERE UPPER(TRIM(dt.{nameof(DocumentType.Code)})) = UPPER(TRIM(:Code))
                       AND dt.{nameof(DocumentType.IsDeleted)} = 0";

        return await _connection.QuerySingleOrDefaultAsync<DocumentType>(sql, new { Code = code });
    }

    public async Task<(IEnumerable<DocumentType> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? keyword,
        int? status)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sqlBase = $@"FROM DOCUMENT_TYPES dt
                         LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
                         WHERE dt.{nameof(DocumentType.IsDeleted)} = 0";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(keyword))
        {
            sqlBase += $" AND (LOWER(dt.{nameof(DocumentType.Code)}) LIKE :Keyword OR LOWER(dt.{nameof(DocumentType.Name)}) LIKE :Keyword)";
            parameters.Add("Keyword", $"%{keyword.ToLower().Trim()}%");
        }

        if (status.HasValue)
        {
            sqlBase += " AND dt.IS_ACTIVE = :Status";
            parameters.Add("Status", status.Value);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectSql = $@"SELECT dt.{nameof(DocumentType.Id)},
                                   dt.{nameof(DocumentType.Name)},
                                   dt.{nameof(DocumentType.Code)},
                                   dt.FORM_ID as {nameof(DocumentType.FormId)},
                                    dt.IS_ACTIVE as {nameof(DocumentType.IsActive)},
                                    dt.IS_EQUIPMENT_PROFILE as {nameof(DocumentType.IsEquipmentProfile)},
                                    dt.IS_FACTORY_ACCEPTANCE_REPORT as {nameof(DocumentType.IsFactoryAcceptanceReport)},
                                    dt.IS_CBM_DOCUMENT as {nameof(DocumentType.IsCbmDocument)},
                                    dt.PIORITY as {nameof(DocumentType.Piority)},
                                   dt.{nameof(DocumentType.CreatedBy)},
                                   dt.{nameof(DocumentType.CreatedDate)},
                                   dt.{nameof(DocumentType.ModifiedBy)},
                                   dt.{nameof(DocumentType.ModifiedDate)},
                                   dt.{nameof(DocumentType.IsDeleted)},
                                   f.Name as {nameof(DocumentType.FormName)}
                           {sqlBase}
                           ORDER BY dt.PIORITY ASC, dt.{nameof(DocumentType.CreatedDate)} DESC
                           OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var items = await _connection.QueryAsync<DocumentType>(selectSql, parameters);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<DocumentType>> GetActiveByDossierTypeIdAsync(Guid dossierTypeId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT dt.{nameof(DocumentType.Id)},
                            dt.{nameof(DocumentType.Name)},
                            dt.{nameof(DocumentType.Code)},
                            dt.FORM_ID as {nameof(DocumentType.FormId)},
                             dt.IS_ACTIVE as {nameof(DocumentType.IsActive)},
                             dt.IS_EQUIPMENT_PROFILE as {nameof(DocumentType.IsEquipmentProfile)},
                             dt.IS_FACTORY_ACCEPTANCE_REPORT as {nameof(DocumentType.IsFactoryAcceptanceReport)},
                             dt.IS_CBM_DOCUMENT as {nameof(DocumentType.IsCbmDocument)},
                             dt.PIORITY as {nameof(DocumentType.Piority)},
                            dt.{nameof(DocumentType.CreatedBy)},
                            dt.{nameof(DocumentType.CreatedDate)},
                            dt.{nameof(DocumentType.ModifiedBy)},
                            dt.{nameof(DocumentType.ModifiedDate)},
                            dt.{nameof(DocumentType.IsDeleted)},
                            f.Name as {nameof(DocumentType.FormName)}
                     FROM DOCUMENT_TYPES dt
                     INNER JOIN DOSSIER_TYPE_DOCUMENT_TYPES link
                         ON link.DOCUMENT_TYPE_ID = dt.{nameof(DocumentType.Id)}
                     LEFT JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
                     WHERE link.DOSSIER_TYPE_ID = :DossierTypeId
                       AND dt.{nameof(DocumentType.IsDeleted)} = 0
                       AND dt.IS_ACTIVE = 1
                     ORDER BY dt.PIORITY ASC, dt.{nameof(DocumentType.Name)} ASC";

        var items = await _connection.QueryAsync<DocumentType>(
            sql,
            new { DossierTypeId = dossierTypeId.ToString() });
        return items.AsList();
    }

    public async Task<Guid> CreateAsync(DocumentType documentType)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        if (documentType.Id == Guid.Empty)
        {
            documentType.Id = Guid.Parse(EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid());
        }

        var sql = $@"INSERT INTO DOCUMENT_TYPES (
                        {nameof(DocumentType.Id)},
                        {nameof(DocumentType.Name)},
                        {nameof(DocumentType.Code)},
                        FORM_ID,
                        IS_ACTIVE,
                        IS_EQUIPMENT_PROFILE,
                        IS_FACTORY_ACCEPTANCE_REPORT,
                        IS_CBM_DOCUMENT,
                        PIORITY,
                        {nameof(DocumentType.CreatedBy)},
                        {nameof(DocumentType.CreatedDate)},
                        {nameof(DocumentType.IsDeleted)}
                    )
                    VALUES (:Id, :Name, :Code, :FormId, :IsActive, :IsEquipmentProfile, :IsFactoryAcceptanceReport, :IsCbmDocument, :Piority, :CreatedBy, :CreatedDate, :IsDeleted)";

        var param = new
        {
            Id = documentType.Id.ToString(),
            documentType.Name,
            documentType.Code,
            FormId = documentType.FormId?.ToString(),
            IsActive = documentType.IsActive ? 1 : 0,
            IsEquipmentProfile = documentType.IsEquipmentProfile ? 1 : 0,
            IsFactoryAcceptanceReport = documentType.IsFactoryAcceptanceReport ? 1 : 0,
            IsCbmDocument = documentType.IsCbmDocument ? 1 : 0,
            documentType.Piority,
            documentType.CreatedBy,
            documentType.CreatedDate,
            IsDeleted = documentType.IsDeleted ? 1 : 0
        };

        await _connection.ExecuteAsync(sql, param);
        return documentType.Id;
    }

    public async Task<bool> UpdateAsync(DocumentType documentType)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"UPDATE DOCUMENT_TYPES
                    SET {nameof(DocumentType.Name)} = :Name,
                        {nameof(DocumentType.Code)} = :Code,
                        FORM_ID = :FormId,
                         IS_ACTIVE = :IsActive,
                         IS_EQUIPMENT_PROFILE = :IsEquipmentProfile,
                         IS_FACTORY_ACCEPTANCE_REPORT = :IsFactoryAcceptanceReport,
                         IS_CBM_DOCUMENT = :IsCbmDocument,
                         PIORITY = :Piority,
                        {nameof(DocumentType.ModifiedBy)} = :ModifiedBy,
                        {nameof(DocumentType.ModifiedDate)} = :ModifiedDate
                    WHERE {nameof(DocumentType.Id)} = :Id AND {nameof(DocumentType.IsDeleted)} = 0";

        var param = new
        {
            Id = documentType.Id.ToString(),
            documentType.Name,
            documentType.Code,
            FormId = documentType.FormId?.ToString(),
            IsActive = documentType.IsActive ? 1 : 0,
            IsEquipmentProfile = documentType.IsEquipmentProfile ? 1 : 0,
            IsFactoryAcceptanceReport = documentType.IsFactoryAcceptanceReport ? 1 : 0,
            IsCbmDocument = documentType.IsCbmDocument ? 1 : 0,
            documentType.Piority,
            documentType.ModifiedBy,
            documentType.ModifiedDate
        };

        var affected = await _connection.ExecuteAsync(sql, param);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"UPDATE DOCUMENT_TYPES
                    SET {nameof(DocumentType.IsDeleted)} = 1
                    WHERE {nameof(DocumentType.Id)} = :Id";

        var affected = await _connection.ExecuteAsync(sql, new { Id = id.ToString() });
        return affected > 0;
    }
}
