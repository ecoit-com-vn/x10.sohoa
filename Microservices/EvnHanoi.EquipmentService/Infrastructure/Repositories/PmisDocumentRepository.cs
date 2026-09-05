using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class PmisDocumentRepository : IPmisDocumentRepository
{
    private readonly IDbConnection _connection;

    public PmisDocumentRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<bool> ExistsByCodeAsync(string pmisDocumentCode)
    {
        EnsureOpen();
        var count = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM PMIS_DOCUMENT WHERE PmisDocumentCode = :Code AND IsDeleted = 0",
            new { Code = pmisDocumentCode });
        return count > 0;
    }

    public async Task<Guid?> ResolveOwnerIdAsync(string ownerType, string ownerPmisCode)
    {
        EnsureOpen();

        var sql = ownerType == "INFRASTRUCTURE"
            ? "SELECT Id FROM INFRASTRUCTURE WHERE PMIS_CODE = :Code AND IsDeleted = 0"
            : "SELECT Id FROM EQUIPMENTS WHERE PMIS_CODE = :Code AND IsDeleted = 0";

        var id = await _connection.QuerySingleOrDefaultAsync<string?>(sql, new { Code = ownerPmisCode });
        return id != null ? Guid.Parse(id) : null;
    }

    public async Task InsertAsync(UpsertPmisDocumentRequest item, Guid ownerId, string? objectKey, long? fileSize)
    {
        EnsureOpen();

        const string sql = @"
            INSERT INTO PMIS_DOCUMENT (
                Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, CreatedBy
            ) VALUES (
                :Id, :PmisDocumentCode, :OwnerType, :OwnerId, :DocumentName, :DocumentType, :ObjectKey, :FileSize, 'PMIS_SYNC'
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid(),
            PmisDocumentCode = item.PmisDocumentCode,
            OwnerType = item.OwnerType,
            OwnerId = ownerId.ToString(),
            DocumentName = item.DocumentName,
            DocumentType = item.DocumentType,
            ObjectKey = objectKey,
            FileSize = fileSize
        });
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
