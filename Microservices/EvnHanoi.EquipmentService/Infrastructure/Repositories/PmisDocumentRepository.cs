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
            @"SELECT Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncedAt, CreatedBy
              FROM PMIS_DOCUMENT WHERE Id = :Id AND IsDeleted = 0",
            new { Id = id.ToString() });
        return row == null ? null : ToDetail(row);
    }

    public async Task<Guid> InsertManualAsync(
        string ownerType, Guid ownerId, string documentName, string? documentType, string objectKey, long fileSize, string uploadedBy)
    {
        EnsureOpen();

        var id = Guid.NewGuid();
        // Mã tự sinh, KHÔNG trùng mã PMIS thật (chỉ toàn số/chữ theo quy ước PMIS) — vẫn thoả
        // UQ_PMIS_DOCUMENT_CODE, và tiền tố "MANUAL_" cho phép nhận ra ngay khi cần tra cứu thủ công.
        var pmisDocumentCode = $"MANUAL_{id:N}";

        await _connection.ExecuteAsync(@"
            INSERT INTO PMIS_DOCUMENT (
                Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, CreatedBy
            ) VALUES (
                :Id, :PmisDocumentCode, :OwnerType, :OwnerId, :DocumentName, :DocumentType, :ObjectKey, :FileSize, :CreatedBy
            )",
            new
            {
                Id = id.ToString(),
                PmisDocumentCode = pmisDocumentCode,
                OwnerType = ownerType,
                OwnerId = ownerId.ToString(),
                DocumentName = documentName,
                DocumentType = documentType,
                ObjectKey = objectKey,
                FileSize = fileSize,
                CreatedBy = uploadedBy
            });

        return id;
    }

    private const string UnassignedUnitNodeId = "unit_unassigned";

    public async Task<IReadOnlyList<PmisDocumentCatalogNodeDto>> GetCatalogUnitsAsync()
    {
        EnsureOpen();

        // Chỉ kiểm tra sự tồn tại (EXISTS) thay vì tổng hợp/tải toàn bộ INFRASTRUCTURE + EQUIPMENTS như
        // GetCatalogTreeAsync cũ — cấp gốc chỉ cần biết đơn vị nào có dữ liệu, chưa cần load Trạm/Đường
        // dây/Thiết bị bên trong (sẽ load lười ở GetCatalogUnitChildrenAsync khi người dùng mở đơn vị đó).
        var units = (await _connection.QueryAsync<UnitCatalogRow>(@"
            SELECT ou.Id, ou.Name
            FROM ORGANIZATION_UNIT ou
            WHERE EXISTS (
                SELECT 1 FROM INFRASTRUCTURE i
                WHERE i.UNIT_ID = ou.Id AND i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
                  AND (
                    EXISTS (SELECT 1 FROM PMIS_DOCUMENT pd WHERE pd.OwnerType = 'INFRASTRUCTURE' AND pd.OwnerId = i.Id AND pd.IsDeleted = 0)
                    OR EXISTS (
                        SELECT 1 FROM EQUIPMENTS e
                        INNER JOIN PMIS_DOCUMENT pd2 ON pd2.OwnerType = 'EQUIPMENT' AND pd2.OwnerId = e.Id AND pd2.IsDeleted = 0
                        WHERE e.INFRASTRUCTURE_ID = i.Id AND e.IsDeleted = 0
                    )
                  )
            )"))
            .ToList();

        var nodes = units
            .Select(u => new PmisDocumentCatalogNodeDto { Id = $"unit_{u.Id}", Name = u.Name, ParentId = null, NodeType = "unit" })
            .ToList();

        // Trạm/Đường dây chưa xác định được đơn vị (PMIS_UNIT_CODE_MAPPING thiếu mã đơn vị) — KHÔNG bỏ
        // qua (sẽ làm mất luôn thiết bị/tài liệu con khỏi cây, không có cách nào khác để tìm thấy), gom
        // vào 1 node "đơn vị" tạm ở gốc cây để vẫn duyệt/xem/chọn được, kèm gợi ý cho admin đi sửa mapping.
        var hasUnassigned = await _connection.ExecuteScalarAsync<int>(@"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFRASTRUCTURE i
                WHERE i.UNIT_ID IS NULL AND i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
                  AND (
                    EXISTS (SELECT 1 FROM PMIS_DOCUMENT pd WHERE pd.OwnerType = 'INFRASTRUCTURE' AND pd.OwnerId = i.Id AND pd.IsDeleted = 0)
                    OR EXISTS (
                        SELECT 1 FROM EQUIPMENTS e
                        INNER JOIN PMIS_DOCUMENT pd2 ON pd2.OwnerType = 'EQUIPMENT' AND pd2.OwnerId = e.Id AND pd2.IsDeleted = 0
                        WHERE e.INFRASTRUCTURE_ID = i.Id AND e.IsDeleted = 0
                    )
                  )
            ) THEN 1 ELSE 0 END
            FROM DUAL") > 0;

        if (hasUnassigned)
        {
            nodes.Add(new PmisDocumentCatalogNodeDto
            {
                Id = UnassignedUnitNodeId,
                Name = "(Chưa xác định đơn vị — kiểm tra PMIS_UNIT_CODE_MAPPING)",
                ParentId = null,
                NodeType = "unit"
            });
        }

        return nodes;
    }

    public async Task<IReadOnlyList<PmisDocumentCatalogNodeDto>> GetCatalogUnitChildrenAsync(string unitNodeId)
    {
        EnsureOpen();

        long? unitId = null;
        if (!string.Equals(unitNodeId, UnassignedUnitNodeId, StringComparison.OrdinalIgnoreCase))
        {
            var rawId = unitNodeId.StartsWith("unit_", StringComparison.OrdinalIgnoreCase) ? unitNodeId["unit_".Length..] : unitNodeId;
            if (!long.TryParse(rawId, out var parsedUnitId))
                return Array.Empty<PmisDocumentCatalogNodeDto>();
            unitId = parsedUnitId;
        }

        // Giữ nguyên cách đếm tài liệu bằng GROUP BY 1 lần rồi LEFT/INNER JOIN (xem lịch sử 504 timeout ở
        // GetCatalogTreeAsync cũ), chỉ thêm điều kiện lọc theo đúng 1 đơn vị (UnitId) để không còn phải
        // quét toàn bộ INFRASTRUCTURE/EQUIPMENTS của mọi đơn vị trong 1 lần gọi.
        var infraRows = (await _connection.QueryAsync<InfraCatalogRow>(@"
            SELECT i.Id, i.Name, i.Code, i.INFRA_TYPE_ID AS InfraTypeId, i.UNIT_ID AS UnitId,
                   NVL(direct_doc.DocCount, 0) AS DirectDocumentCount,
                   NVL(child_doc.DocCount, 0) AS ChildEquipmentDocumentCount
            FROM INFRASTRUCTURE i
            LEFT JOIN (
                SELECT OwnerId, COUNT(1) AS DocCount
                FROM PMIS_DOCUMENT
                WHERE OwnerType = 'INFRASTRUCTURE' AND IsDeleted = 0
                GROUP BY OwnerId
            ) direct_doc ON direct_doc.OwnerId = i.Id
            LEFT JOIN (
                SELECT e.INFRASTRUCTURE_ID AS InfrastructureId, COUNT(1) AS DocCount
                FROM EQUIPMENTS e
                INNER JOIN PMIS_DOCUMENT pd ON pd.OwnerType = 'EQUIPMENT' AND pd.OwnerId = e.Id AND pd.IsDeleted = 0
                WHERE e.IsDeleted = 0
                GROUP BY e.INFRASTRUCTURE_ID
            ) child_doc ON child_doc.InfrastructureId = i.Id
            WHERE i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
              AND (direct_doc.DocCount IS NOT NULL OR child_doc.DocCount IS NOT NULL)
              AND ((:UnitId IS NULL AND i.UNIT_ID IS NULL) OR i.UNIT_ID = :UnitId)",
            new { UnitId = unitId }))
            .ToList();

        var nodes = new List<PmisDocumentCatalogNodeDto>();
        if (infraRows.Count == 0) return nodes;

        var infraIds = infraRows.Select(r => r.Id).ToList();
        var equipmentRows = (await _connection.QueryAsync<EquipmentCatalogRow>(@"
            SELECT e.Id, e.Name, e.Code, e.INFRASTRUCTURE_ID AS InfrastructureId, doc.DocCount AS DocumentCount
            FROM EQUIPMENTS e
            INNER JOIN (
                SELECT OwnerId, COUNT(1) AS DocCount
                FROM PMIS_DOCUMENT
                WHERE OwnerType = 'EQUIPMENT' AND IsDeleted = 0
                GROUP BY OwnerId
            ) doc ON doc.OwnerId = e.Id
            WHERE e.PMIS_CODE IS NOT NULL AND e.IsDeleted = 0
              AND e.INFRASTRUCTURE_ID IN :InfraIds",
            new { InfraIds = infraIds }))
            .ToList();

        foreach (var infra in infraRows)
        {
            nodes.Add(new PmisDocumentCatalogNodeDto
            {
                Id = $"infra_{infra.Id}",
                Name = string.IsNullOrEmpty(infra.Code) ? infra.Name : $"{infra.Name} ({infra.Code})",
                ParentId = unitNodeId,
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
            $@"SELECT Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncedAt, CreatedBy
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
        SyncedAt = row.SyncedAt,
        IsManual = !string.Equals(row.CreatedBy, "PMIS_SYNC", StringComparison.OrdinalIgnoreCase)
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
        public string? CreatedBy { get; set; }
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
