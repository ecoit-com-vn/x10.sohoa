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

    public async Task<PmisDocumentDetail?> GetByIdAsync(Guid id)
    {
        EnsureOpen();
        var row = await _connection.QuerySingleOrDefaultAsync<PmisDocumentRow>(
            @"SELECT Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncedAt
              FROM PMIS_DOCUMENT WHERE Id = :Id AND IsDeleted = 0",
            new { Id = id.ToString() });
        return row == null ? null : ToDetail(row);
    }

    public async Task<IReadOnlyList<PmisDocumentCatalogNodeDto>> GetCatalogTreeAsync()
    {
        EnsureOpen();

        var infraRows = (await _connection.QueryAsync<InfraCatalogRow>(@"
            SELECT i.Id, i.Name, i.Code, i.INFRA_TYPE_ID AS InfraTypeId, i.UnitId,
                   (SELECT COUNT(1) FROM PMIS_DOCUMENT pd
                      WHERE pd.OwnerType = 'INFRASTRUCTURE' AND pd.OwnerId = i.Id AND pd.IsDeleted = 0) AS DirectDocumentCount,
                   (SELECT COUNT(1) FROM EQUIPMENTS e
                      INNER JOIN PMIS_DOCUMENT pd2 ON pd2.OwnerType = 'EQUIPMENT' AND pd2.OwnerId = e.Id AND pd2.IsDeleted = 0
                    WHERE e.INFRASTRUCTURE_ID = i.Id AND e.IsDeleted = 0) AS ChildEquipmentDocumentCount
            FROM INFRASTRUCTURE i
            WHERE i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0"))
            .Where(r => r.DirectDocumentCount > 0 || r.ChildEquipmentDocumentCount > 0)
            .ToList();

        var nodes = new List<PmisDocumentCatalogNodeDto>();
        if (infraRows.Count == 0) return nodes;

        var infraIds = infraRows.Select(r => r.Id).ToHashSet();
        var equipmentRows = (await _connection.QueryAsync<EquipmentCatalogRow>(@"
            SELECT e.Id, e.Name, e.Code, e.INFRASTRUCTURE_ID AS InfrastructureId,
                   (SELECT COUNT(1) FROM PMIS_DOCUMENT pd
                      WHERE pd.OwnerType = 'EQUIPMENT' AND pd.OwnerId = e.Id AND pd.IsDeleted = 0) AS DocumentCount
            FROM EQUIPMENTS e
            WHERE e.PMIS_CODE IS NOT NULL AND e.IsDeleted = 0
              AND EXISTS (SELECT 1 FROM PMIS_DOCUMENT pd
                          WHERE pd.OwnerType = 'EQUIPMENT' AND pd.OwnerId = e.Id AND pd.IsDeleted = 0)"))
            .Where(r => r.InfrastructureId != null && infraIds.Contains(r.InfrastructureId!))
            .ToList();

        var unitIds = infraRows.Where(r => r.UnitId.HasValue).Select(r => r.UnitId!.Value).Distinct().ToList();
        var units = unitIds.Count == 0
            ? new List<UnitCatalogRow>()
            : (await _connection.QueryAsync<UnitCatalogRow>(
                "SELECT Id, Name FROM ORGANIZATION_UNIT WHERE Id IN :UnitIds", new { UnitIds = unitIds })).ToList();

        foreach (var unit in units)
        {
            nodes.Add(new PmisDocumentCatalogNodeDto { Id = $"unit_{unit.Id}", Name = unit.Name, ParentId = null, NodeType = "unit" });
        }

        // Trạm/Đường dây chưa xác định được đơn vị (PMIS_UNIT_CODE_MAPPING thiếu mã đơn vị) — KHÔNG bỏ
        // qua (sẽ làm mất luôn thiết bị/tài liệu con khỏi cây, không có cách nào khác để tìm thấy), gom
        // vào 1 node "đơn vị" tạm ở gốc cây để vẫn duyệt/xem/chọn được, kèm gợi ý cho admin đi sửa mapping.
        const string unassignedUnitId = "unit_unassigned";
        var hasUnassigned = infraRows.Any(r => !r.UnitId.HasValue);
        if (hasUnassigned)
        {
            nodes.Add(new PmisDocumentCatalogNodeDto
            {
                Id = unassignedUnitId,
                Name = "(Chưa xác định đơn vị — kiểm tra PMIS_UNIT_CODE_MAPPING)",
                ParentId = null,
                NodeType = "unit"
            });
        }

        foreach (var infra in infraRows)
        {
            var parentUnitNodeId = infra.UnitId.HasValue ? $"unit_{infra.UnitId}" : unassignedUnitId;

            nodes.Add(new PmisDocumentCatalogNodeDto
            {
                Id = $"infra_{infra.Id}",
                Name = string.IsNullOrEmpty(infra.Code) ? infra.Name : $"{infra.Name} ({infra.Code})",
                ParentId = parentUnitNodeId,
                NodeType = infra.InfraTypeId == 1 ? "substation" : "line",
                DocumentCount = infra.DirectDocumentCount
            });
        }

        foreach (var eq in equipmentRows)
        {
            nodes.Add(new PmisDocumentCatalogNodeDto
            {
                Id = $"equipment_{eq.Id}",
                Name = string.IsNullOrEmpty(eq.Code) ? eq.Name : $"{eq.Name} ({eq.Code})",
                ParentId = $"infra_{eq.InfrastructureId}",
                NodeType = "equipment",
                DocumentCount = eq.DocumentCount
            });
        }

        return nodes;
    }

    public async Task<(IEnumerable<PmisDocumentDetail> Items, int TotalCount)> GetByOwnerAsync(
        string ownerType, Guid ownerId, string? keyword, int page, int pageSize)
    {
        EnsureOpen();

        var parameters = new DynamicParameters();
        parameters.Add("OwnerType", ownerType);
        parameters.Add("OwnerId", ownerId.ToString());
        parameters.Add("Keyword", string.IsNullOrWhiteSpace(keyword) ? null : $"%{keyword.ToUpperInvariant()}%");
        parameters.Add("Skip", (page - 1) * pageSize);
        parameters.Add("Take", pageSize);

        const string whereSql = @"WHERE OwnerType = :OwnerType AND OwnerId = :OwnerId AND IsDeleted = 0
                                     AND (:Keyword IS NULL OR UPPER(DocumentName) LIKE :Keyword)";

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM PMIS_DOCUMENT {whereSql}", parameters);

        var rows = await _connection.QueryAsync<PmisDocumentRow>(
            $@"SELECT Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncedAt
               FROM PMIS_DOCUMENT
               {whereSql}
               ORDER BY SyncedAt DESC
               OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY", parameters);

        return (rows.Select(ToDetail), totalCount);
    }

    private static PmisDocumentDetail ToDetail(PmisDocumentRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        PmisDocumentCode = row.PmisDocumentCode,
        OwnerType = row.OwnerType,
        OwnerId = Guid.Parse(row.OwnerId),
        DocumentName = row.DocumentName,
        DocumentType = row.DocumentType,
        ObjectKey = row.ObjectKey,
        FileSize = row.FileSize,
        SyncedAt = row.SyncedAt
    };

    private class PmisDocumentRow
    {
        public string Id { get; set; } = string.Empty;
        public string PmisDocumentCode { get; set; } = string.Empty;
        public string OwnerType { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string? DocumentName { get; set; }
        public string? DocumentType { get; set; }
        public string? ObjectKey { get; set; }
        public long? FileSize { get; set; }
        public DateTime SyncedAt { get; set; }
    }

    private class InfraCatalogRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int InfraTypeId { get; set; }
        public long? UnitId { get; set; }
        public int DirectDocumentCount { get; set; }
        public int ChildEquipmentDocumentCount { get; set; }
    }

    private class EquipmentCatalogRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? InfrastructureId { get; set; }
        public int DocumentCount { get; set; }
    }

    private class UnitCatalogRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
