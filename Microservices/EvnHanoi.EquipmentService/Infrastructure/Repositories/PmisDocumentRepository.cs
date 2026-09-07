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

    public async Task<PmisDocumentLookup?> GetByCodeAsync(string pmisDocumentCode)
    {
        EnsureOpen();
        // KHÔNG lọc IsDeleted: PmisDocumentCode có UQ_PMIS_DOCUMENT_CODE (không loại trừ dòng đã xoá
        // mềm) — nếu lọc IsDeleted=0 ở đây, 1 dòng đã xoá mềm sẽ "vô hình", khiến InsertAsync sau đó
        // đụng đúng constraint này (giống lỗi đã sửa ở EquipmentRepository.ResolveOrCreateEquipmentTypeIdAsync).
        return await _connection.QuerySingleOrDefaultAsync<PmisDocumentLookup>(
            "SELECT Id, ObjectKey FROM PMIS_DOCUMENT WHERE PmisDocumentCode = :Code",
            new { Code = pmisDocumentCode });
    }

    public async Task<Guid?> ResolveOwnerIdAsync(string ownerType, string ownerPmisCode)
    {
        EnsureOpen();

        string sql;
        switch (ownerType)
        {
            case "INFRASTRUCTURE":
                sql = "SELECT Id FROM INFRASTRUCTURE WHERE PMIS_CODE = :Code AND IsDeleted = 0";
                break;
            case "EQUIPMENT":
                sql = "SELECT Id FROM EQUIPMENTS WHERE PMIS_CODE = :Code AND IsDeleted = 0";
                break;
            default:
                return null;
        }

        var id = await _connection.QuerySingleOrDefaultAsync<string?>(sql, new { Code = ownerPmisCode });
        return id != null ? Guid.Parse(id) : null;
    }

    public async Task InsertAsync(UpsertPmisDocumentRequest item, Guid ownerId, string? objectKey, long? fileSize)
    {
        EnsureOpen();

        const string sql = @"
            INSERT INTO PMIS_DOCUMENT (
                Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncHistoryId, CreatedBy
            ) VALUES (
                :Id, :PmisDocumentCode, :OwnerType, :OwnerId, :DocumentName, :DocumentType, :ObjectKey, :FileSize, :SyncHistoryId, 'PMIS_SYNC'
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
            FileSize = fileSize,
            SyncHistoryId = item.SyncHistoryId
        });
    }

    public async Task UpdateFileAsync(string id, string objectKey, long fileSize, string? syncHistoryId)
    {
        EnsureOpen();
        // IsDeleted = 0: khôi phục nếu dòng đang bị xoá mềm (xem comment ở GetByCodeAsync) — vô hại nếu
        // dòng đang active sẵn.
        const string sql = @"
            UPDATE PMIS_DOCUMENT
            SET ObjectKey = :ObjectKey, FileSize = :FileSize, SyncHistoryId = :SyncHistoryId,
                SyncedAt = SYSTIMESTAMP, ModifiedBy = 'PMIS_SYNC', ModifiedDate = SYSTIMESTAMP, IsDeleted = 0
            WHERE Id = :Id";
        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            ObjectKey = objectKey,
            FileSize = fileSize,
            SyncHistoryId = syncHistoryId
        });
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
