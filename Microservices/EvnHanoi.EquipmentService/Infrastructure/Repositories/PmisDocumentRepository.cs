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
        // OwnerType/OwnerId lấy kèm để caller so sánh với chủ sở hữu resolve lại được (xem
        // InternalPmisSyncController.UpsertDocumentsFromPmis — sửa owner sai do đồng bộ trước khi thiết
        // bị thật tồn tại, không chỉ dựa vào Id/ObjectKey như trước).
        return await _connection.QuerySingleOrDefaultAsync<PmisDocumentLookup>(
            "SELECT Id, ObjectKey, OwnerType, OwnerId FROM PMIS_DOCUMENT WHERE PmisDocumentCode = :Code",
            new { Code = pmisDocumentCode });
    }

    public async Task<Guid?> ResolveOwnerIdAsync(string ownerType, string ownerPmisCode)
    {
        EnsureOpen();

        string sql;
        switch (ownerType)
        {
            case "INFRASTRUCTURE":
                // UPPER(TRIM(...)) — khớp đúng cách so sánh chuẩn hoá đã dùng ở InfrastructureRepository/
                // EquipmentRepository.UpsertFromPmisAsync (PMIS trả PMIS_CODE lệch khoảng trắng/hoa-thường
                // tuỳ lần) — trước đây so khớp CHÍNH XÁC ở đây khiến tài liệu báo "Không tìm thấy đối
                // tượng sở hữu" dù Trạm/Đường dây/Thiết bị đó thật sự đã tồn tại, chỉ lệch định dạng mã.
                sql = "SELECT Id FROM INFRASTRUCTURE WHERE UPPER(TRIM(PMIS_CODE)) = UPPER(TRIM(:Code)) AND IsDeleted = 0";
                break;
            case "EQUIPMENT":
                // EquipmentSqlFilters.NotTransferredAway — loại "hồn ma" chuyển TBA, giữ thiết bị "đã
                // chuyển hồ sơ" (xem GetPagedAsync).
                sql = $"SELECT Id FROM EQUIPMENTS WHERE UPPER(TRIM(PMIS_CODE)) = UPPER(TRIM(:Code)) AND IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway()}";
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
                Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncHistoryId, CreatedBy,
                FILE_STATUS, DEVICE_CODE
            ) VALUES (
                :Id, :PmisDocumentCode, :OwnerType, :OwnerId, :DocumentName, :DocumentType, :ObjectKey, :FileSize, :SyncHistoryId, 'PMIS_SYNC',
                :FileStatus, :DeviceCode
            )";

        // FILE_URL/FILE_SOURCE_API không còn ghi (PMIS bỏ link; file tải theo mã qua DOCUMENT_FILE_DOWNLOAD) — cột giữ lại, để NULL.
        await _connection.ExecuteAsync(sql, new
        {
            Id = EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid(),
            PmisDocumentCode = item.PmisDocumentCode,
            OwnerType = item.OwnerType,
            OwnerId = ownerId.ToString(),
            DocumentName = Truncate(item.DocumentName, 500),
            DocumentType = Truncate(item.DocumentType, 200),
            ObjectKey = objectKey,
            FileSize = fileSize,
            SyncHistoryId = item.SyncHistoryId,
            FileStatus = objectKey != null ? "DONE" : "PENDING",
            DeviceCode = NormalizeDeviceCode(item.DeviceCode)
        });
    }

    public async Task EnsureFilePendingAsync(string id)
    {
        EnsureOpen();
        await _connection.ExecuteAsync(@"
            UPDATE PMIS_DOCUMENT
            SET FILE_STATUS = 'PENDING', FILE_ATTEMPTS = 0, FILE_NEXT_RETRY_AT = NULL
            WHERE Id = :Id AND ObjectKey IS NULL AND FILE_STATUS = 'NO_URL'", new { Id = id });
    }

    /// <summary>Mỗi tiền tố loại 1 điều kiện NOT LIKE (escape % _ \\); số tiền tố nhỏ nên ghép tham số theo chỉ số là đủ.</summary>
    private static (string Sql, DynamicParameters Parameters) BuildExcludeClause(IReadOnlyList<string>? excludePrefixes)
    {
        var sb = new System.Text.StringBuilder();
        var parameters = new DynamicParameters();
        var prefixes = (excludePrefixes ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Take(10).ToList();
        for (var i = 0; i < prefixes.Count; i++)
        {
            sb.Append($" AND PmisDocumentCode NOT LIKE :Ex{i} ESCAPE '\\'");
            parameters.Add($"Ex{i}", prefixes[i].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%");
        }
        return (sb.ToString(), parameters);
    }

    public async Task<IReadOnlyList<PendingPmisDocumentFile>> GetPendingFilesAsync(int take, IReadOnlyList<string>? excludePrefixes = null)
    {
        EnsureOpen();
        var (excludeSql, parameters) = BuildExcludeClause(excludePrefixes);
        parameters.Add("Take", take);

        var sql = $@"
            SELECT PmisDocumentCode, FILE_ATTEMPTS AS FileAttempts
            FROM PMIS_DOCUMENT
            WHERE ObjectKey IS NULL AND IsDeleted = 0
              AND FILE_STATUS IN ('PENDING', 'FAILED')
              AND (FILE_NEXT_RETRY_AT IS NULL OR FILE_NEXT_RETRY_AT <= SYSTIMESTAMP){excludeSql}
            ORDER BY FILE_ATTEMPTS, CreatedDate
            FETCH FIRST :Take ROWS ONLY";
        return (await _connection.QueryAsync<PendingPmisDocumentFile>(sql, parameters)).ToList();
    }

    // Dùng SyncedAt (không phải ModifiedDate) để tính LastDownloadedAt bên dưới — ModifiedDate còn bị
    // UpdateOwnerAsync bump lại trên CẢ dòng đã DONE từ trước (khi 1 lượt đồng bộ Thiết bị khác resolve
    // lại đúng owner hơn, không liên quan gì tới việc tải file), khiến watchdog tưởng vừa có hoạt động dù
    // PmisDocumentFileDownloadJob đã chết thật. SyncedAt chỉ được set đúng 1 chỗ duy nhất (UpdateFileAsync,
    // ngay khi 1 file được tải và lưu thành công thật) nên không bị lẫn như vậy.
    public async Task<PendingDocumentFileSummary> GetPendingSummaryAsync(IReadOnlyList<string>? excludePrefixes = null)
    {
        EnsureOpen();
        var (excludeSql, parameters) = BuildExcludeClause(excludePrefixes);
        var sql = $@"
            SELECT
                (SELECT COUNT(*) FROM PMIS_DOCUMENT
                  WHERE ObjectKey IS NULL AND IsDeleted = 0
                    AND FILE_STATUS IN ('PENDING', 'FAILED')
                    AND (FILE_NEXT_RETRY_AT IS NULL OR FILE_NEXT_RETRY_AT <= SYSTIMESTAMP){excludeSql}) AS PendingCount,
                (SELECT MAX(SyncedAt) FROM PMIS_DOCUMENT WHERE FILE_STATUS = 'DONE') AS LastDownloadedAt
            FROM DUAL";
        return await _connection.QuerySingleAsync<PendingDocumentFileSummary>(sql, parameters);
    }

    public async Task<IReadOnlyList<PmisDocumentFileStatus>> GetFileStatusByCodesAsync(IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return [];
        EnsureOpen();
        const string sql = @"
            SELECT PmisDocumentCode, FILE_STATUS AS FileStatus, FILE_ATTEMPTS AS FileAttempts,
                   FILE_LAST_ERROR AS FileLastError,
                   CASE WHEN ObjectKey IS NOT NULL THEN 1 ELSE 0 END AS HasFile
            FROM PMIS_DOCUMENT
            WHERE IsDeleted = 0 AND PmisDocumentCode IN :Codes";
        return (await _connection.QueryAsync<PmisDocumentFileStatus>(sql, new { Codes = codes })).ToList();
    }

    public async Task<IReadOnlyList<PendingOwnerInfrastructure>> GetPendingOwnerInfrastructuresAsync()
    {
        EnsureOpen();
        // Tài liệu của THIẾT BỊ quy về Trạm/Đường dây chứa nó (EQUIPMENTS.INFRASTRUCTURE_ID).
        const string sql = @"
            SELECT DISTINCT i.PMIS_CODE AS PmisCode, i.INFRA_TYPE_ID AS InfraTypeId
            FROM PMIS_DOCUMENT d
            LEFT JOIN EQUIPMENTS e ON d.OwnerType = 'EQUIPMENT' AND e.Id = d.OwnerId
            JOIN INFRASTRUCTURE i ON i.Id = CASE WHEN d.OwnerType = 'INFRASTRUCTURE' THEN d.OwnerId ELSE e.INFRASTRUCTURE_ID END
            WHERE d.ObjectKey IS NULL AND d.IsDeleted = 0
              AND d.FILE_STATUS IN ('PENDING', 'FAILED', 'NO_URL')
              AND i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
            ORDER BY i.PMIS_CODE";
        return (await _connection.QueryAsync<PendingOwnerInfrastructure>(sql)).ToList();
    }

    public async Task<PmisDocumentFileTarget?> GetFileTargetByCodeAsync(string pmisDocumentCode)
    {
        EnsureOpen();
        return await _connection.QuerySingleOrDefaultAsync<PmisDocumentFileTarget>(
            @"SELECT Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, ObjectKey
              FROM PMIS_DOCUMENT WHERE PmisDocumentCode = :Code AND IsDeleted = 0",
            new { Code = pmisDocumentCode });
    }

    public async Task MarkFileFailedAsync(string id, string? errorMessage)
    {
        EnsureOpen();
        // Backoff luỹ thừa 1,2,4,...,128 phút (tối đa 8 lần); từ lần thứ 8 trở đi FAILED, thử lại mỗi 24 giờ.
        const string sql = @"
            UPDATE PMIS_DOCUMENT
            SET FILE_ATTEMPTS = FILE_ATTEMPTS + 1, FILE_TRANSIENT_FAILS = 0,
                FILE_LAST_ERROR = :ErrorText,
                FILE_STATUS = CASE WHEN FILE_ATTEMPTS + 1 >= 8 THEN 'FAILED' ELSE 'PENDING' END,
                FILE_NEXT_RETRY_AT = SYSTIMESTAMP + CASE WHEN FILE_ATTEMPTS + 1 >= 8 THEN INTERVAL '24' HOUR
                                                         ELSE NUMTODSINTERVAL(POWER(2, FILE_ATTEMPTS), 'MINUTE') END,
                ModifiedBy = 'PMIS_SYNC', ModifiedDate = SYSTIMESTAMP
            WHERE Id = :Id AND ObjectKey IS NULL";
        var err = errorMessage is { Length: > 1900 } ? errorMessage[..1900] : errorMessage;
        await _connection.ExecuteAsync(sql, new { ErrorText = err, Id = id });
    }

    public async Task<IReadOnlyList<DocumentCountByInfrastructure>> GetDocumentCountsByInfrastructureAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT i.PMIS_CODE AS PmisCode, COUNT(*) AS DocumentCount
            FROM (
                SELECT d.OwnerId AS InfraId FROM PMIS_DOCUMENT d
                 WHERE d.IsDeleted = 0 AND d.OwnerType = 'INFRASTRUCTURE' AND d.PmisDocumentCode NOT LIKE 'MANUAL\_%' ESCAPE '\'
                UNION ALL
                SELECT e.INFRASTRUCTURE_ID AS InfraId FROM PMIS_DOCUMENT d
                  JOIN EQUIPMENTS e ON e.Id = d.OwnerId
                 WHERE d.IsDeleted = 0 AND d.OwnerType = 'EQUIPMENT' AND d.PmisDocumentCode NOT LIKE 'MANUAL\_%' ESCAPE '\'
            ) x
            JOIN INFRASTRUCTURE i ON i.Id = x.InfraId
            WHERE i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
            GROUP BY i.PMIS_CODE";
        return (await _connection.QueryAsync<DocumentCountByInfrastructure>(sql, commandTimeout: 300)).ToList();
    }

    public async Task SetDeviceCodeAsync(string id, string deviceCode)
    {
        EnsureOpen();
        await _connection.ExecuteAsync(
            "UPDATE PMIS_DOCUMENT SET DEVICE_CODE = :DeviceCode WHERE Id = :Id AND (DEVICE_CODE IS NULL OR DEVICE_CODE <> :DeviceCode2)",
            new { DeviceCode = NormalizeDeviceCode(deviceCode), Id = id, DeviceCode2 = NormalizeDeviceCode(deviceCode) });
    }

    public async Task<int> ReassignDocumentsToEquipmentAsync(string equipmentPmisCode, Guid equipmentId)
    {
        EnsureOpen();
        // Thiết bị vừa được tạo: nhận lại các tài liệu từng lưu tạm cho Trạm/Đường dây vì lúc đó thiết bị chưa tồn tại (khớp DEVICE_CODE đã lưu).
        return await _connection.ExecuteAsync(@"
            UPDATE PMIS_DOCUMENT
            SET OwnerType = 'EQUIPMENT', OwnerId = :EquipmentId, ModifiedBy = 'PMIS_SYNC', ModifiedDate = SYSTIMESTAMP
            WHERE DEVICE_CODE = :DeviceCode AND OwnerType = 'INFRASTRUCTURE' AND IsDeleted = 0",
            new { EquipmentId = equipmentId.ToString(), DeviceCode = NormalizeDeviceCode(equipmentPmisCode) });
    }

    public async Task<int> MoveDocumentsBetweenEquipmentAsync(Guid oldEquipmentId, Guid newEquipmentId)
    {
        EnsureOpen();
        // Chuyển TBA tự động: thiết bị cũ thành "hồn ma" (ẩn khỏi danh mục), thiết bị mới nhận hồ sơ — tài liệu phải đi theo thiết bị đang sống.
        return await _connection.ExecuteAsync(@"
            UPDATE PMIS_DOCUMENT
            SET OwnerId = :NewId, ModifiedBy = 'PMIS_SYNC', ModifiedDate = SYSTIMESTAMP
            WHERE OwnerType = 'EQUIPMENT' AND OwnerId = :OldId AND IsDeleted = 0",
            new { NewId = newEquipmentId.ToString(), OldId = oldEquipmentId.ToString() });
    }

    /// <summary>Mã thiết bị chuẩn hoá để so khớp: UPPER(TRIM) — cùng cách ResolveOwnerIdAsync so PMIS_CODE.</summary>
    private static string? NormalizeDeviceCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static string? Truncate(string? value, int max) =>
        value is { } v && v.Length > max ? v[..max] : value;

    public async Task<string?> FindObjectKeyByHashAsync(string contentSha256, long fileSize)
    {
        EnsureOpen();
        return await _connection.QueryFirstOrDefaultAsync<string?>(@"
            SELECT ObjectKey FROM PMIS_DOCUMENT
            WHERE CONTENT_SHA256 = :ContentSha AND FileSize = :ContentSize AND ObjectKey IS NOT NULL AND IsDeleted = 0
            FETCH FIRST 1 ROWS ONLY", new { ContentSha = contentSha256, ContentSize = fileSize });
    }

    /// <summary>Số lần tải lỗi TẠM THỜI liên tiếp đạt ngưỡng này thì coi là lỗi của chính tài liệu (tính vào FILE_ATTEMPTS, backoff dài).</summary>
    private const int MaxTransientFailures = 12;

    public async Task MarkFileTransientFailureAsync(string id, string? errorMessage, int retryMinutes)
    {
        EnsureOpen();
        // KHÔNG đụng FILE_ATTEMPTS/FILE_STATUS: PMIS quá tải không phải lỗi của tài liệu. Lùi dần theo số lần liên tiếp (tối đa 6×) + jitter
        // 0-5 phút để không dồn cả lô. Đủ MaxTransientFailures lần liên tiếp (tài liệu luôn làm PMIS lỗi 5xx) → chuyển thành lỗi của tài liệu.
        const string sql = @"
            UPDATE PMIS_DOCUMENT
            SET FILE_TRANSIENT_FAILS = FILE_TRANSIENT_FAILS + 1,
                FILE_LAST_ERROR = :ErrorText,
                FILE_NEXT_RETRY_AT = SYSTIMESTAMP + NUMTODSINTERVAL(:RetryMinutes * LEAST(FILE_TRANSIENT_FAILS + 1, 6) + DBMS_RANDOM.VALUE(0, 5), 'MINUTE'),
                ModifiedBy = 'PMIS_SYNC', ModifiedDate = SYSTIMESTAMP
            WHERE Id = :Id AND ObjectKey IS NULL";
        var err = errorMessage is { Length: > 1880 } ? "[tạm thời] " + errorMessage[..1880] : "[tạm thời] " + errorMessage;
        await _connection.ExecuteAsync(sql, new { ErrorText = err, RetryMinutes = Math.Max(1, retryMinutes), Id = id });

        var fails = await _connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT FILE_TRANSIENT_FAILS FROM PMIS_DOCUMENT WHERE Id = :Id", new { Id = id });
        if (fails >= MaxTransientFailures)
        {
            // MarkFileFailedAsync đồng thời đặt lại FILE_TRANSIENT_FAILS = 0 trong cùng câu UPDATE (không còn bước reset riêng dễ hỏng giữa chừng).
            await MarkFileFailedAsync(id, $"PMIS lỗi tạm thời {fails} lần liên tiếp — coi là lỗi của tài liệu. Lỗi cuối: {errorMessage}");
        }
    }

    public async Task UpdateFileAsync(string id, string objectKey, long fileSize, string? syncHistoryId, string? contentSha256 = null)
    {
        EnsureOpen();
        // IsDeleted = 0: khôi phục nếu dòng đang bị xoá mềm (xem comment ở GetByCodeAsync) — vô hại nếu
        // dòng đang active sẵn.
        const string sql = @"
            UPDATE PMIS_DOCUMENT
            SET ObjectKey = :ObjectKey, FileSize = :FileSize, SyncHistoryId = COALESCE(:SyncHistoryId, SyncHistoryId),
                FILE_STATUS = 'DONE', FILE_LAST_ERROR = NULL, FILE_NEXT_RETRY_AT = NULL,
                CONTENT_SHA256 = COALESCE(:ContentSha256, CONTENT_SHA256), FILE_TRANSIENT_FAILS = 0,
                SyncedAt = SYSTIMESTAMP, ModifiedBy = 'PMIS_SYNC', ModifiedDate = SYSTIMESTAMP, IsDeleted = 0
            WHERE Id = :Id";
        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            ObjectKey = objectKey,
            FileSize = fileSize,
            ContentSha256 = contentSha256,
            SyncHistoryId = syncHistoryId
        });
    }

    public async Task UpdateOwnerAsync(string id, string ownerType, Guid ownerId)
    {
        EnsureOpen();
        // Sửa lại chủ sở hữu 1 dòng đã lưu (kể cả đã có file) khi lượt đồng bộ NÀY resolve ra được chủ
        // đúng hơn (vd EQUIPMENT thật vừa được tạo ở 1 lượt Equipment sync trước đó) — KHÔNG đụng
        // ObjectKey/FileSize, chỉ sửa owner (xem InternalPmisSyncController.UpsertDocumentsFromPmis).
        const string sql = @"
            UPDATE PMIS_DOCUMENT
            SET OwnerType = :OwnerType, OwnerId = :OwnerId, ModifiedBy = 'PMIS_SYNC', ModifiedDate = SYSTIMESTAMP
            WHERE Id = :Id";
        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            OwnerType = ownerType,
            OwnerId = ownerId.ToString()
        });
    }

    public async Task<PmisDocumentDetail?> GetByIdAsync(Guid id)
    {
        EnsureOpen();
        var row = await _connection.QuerySingleOrDefaultAsync<PmisDocumentRow>(
            @"SELECT Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncedAt, CreatedBy,
                     FILE_STATUS AS FileStatus, FILE_LAST_ERROR AS FileLastError
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
                Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, CreatedBy, FILE_STATUS
            ) VALUES (
                :Id, :PmisDocumentCode, :OwnerType, :OwnerId, :DocumentName, :DocumentType, :ObjectKey, :FileSize, :CreatedBy, 'DONE'
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

    public async Task<IReadOnlyList<PmisDocumentCatalogNodeDto>> GetCatalogUnitsAsync(IEnumerable<long>? allowedUnitIds)
    {
        EnsureOpen();

        var allowedIdsList = allowedUnitIds?.ToList();

        // QUAN TRỌNG: KHÔNG dùng EXISTS tương quan (correlated subquery) chạy lại cho từng dòng
        // ORGANIZATION_UNIT — INFRASTRUCTURE.UNIT_ID không có index (chỉ là FK, Oracle không tự tạo index
        // cho FK) nên mỗi lần kiểm tra sẽ full-scan INFRASTRUCTURE 1 lần nữa, nhân với số dòng
        // ORGANIZATION_UNIT (kể cả đơn vị không có dữ liệu) → chậm dù kết quả cuối chỉ vài chục đơn vị.
        // Dùng đúng kỹ thuật GROUP BY 1 lần rồi JOIN như GetCatalogUnitChildrenAsync/GetCatalogTreeAsync
        // cũ (xem lịch sử 504 timeout) — chỉ 1 lượt full-scan duy nhất trên mỗi bảng.
        // EquipmentSqlFilters.NotTransferredAway — loại thiết bị "hồn ma" chuyển TBA khỏi việc quyết định
        // 1 Đơn vị có "còn thiết bị có tài liệu PMIS" hay không (xem GetPagedAsync để biết đầy đủ lý do).
        var unitsSql = $@"
            SELECT DISTINCT ou.Id, ou.Name
            FROM ORGANIZATION_UNIT ou
            INNER JOIN INFRASTRUCTURE i ON i.UNIT_ID = ou.Id AND i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
            LEFT JOIN (
                SELECT OwnerId
                FROM PMIS_DOCUMENT
                WHERE OwnerType = 'INFRASTRUCTURE' AND IsDeleted = 0
                GROUP BY OwnerId
            ) direct_doc ON direct_doc.OwnerId = i.Id
            LEFT JOIN (
                SELECT e.INFRASTRUCTURE_ID AS InfrastructureId
                FROM EQUIPMENTS e
                INNER JOIN PMIS_DOCUMENT pd ON pd.OwnerType = 'EQUIPMENT' AND pd.OwnerId = e.Id AND pd.IsDeleted = 0
                WHERE e.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("e")}
                GROUP BY e.INFRASTRUCTURE_ID
            ) child_doc ON child_doc.InfrastructureId = i.Id
            WHERE (direct_doc.OwnerId IS NOT NULL OR child_doc.InfrastructureId IS NOT NULL)";

        var unitsParameters = new DynamicParameters();
        if (allowedIdsList != null)
        {
            unitsSql += " AND ou.Id IN :AllowedUnitIds";
            unitsParameters.Add("AllowedUnitIds", allowedIdsList.Count > 0 ? allowedIdsList : new List<long> { -1 });
        }

        var units = (await _connection.QueryAsync<UnitCatalogRow>(unitsSql, unitsParameters)).ToList();

        var nodes = units
            .Select(u => new PmisDocumentCatalogNodeDto { Id = $"unit_{u.Id}", Name = u.Name, ParentId = null, NodeType = "unit" })
            .ToList();

        // Trạm/Đường dây chưa xác định được đơn vị (PMIS_UNIT_CODE_MAPPING thiếu mã đơn vị) — KHÔNG bỏ
        // qua (sẽ làm mất luôn thiết bị/tài liệu con khỏi cây, không có cách nào khác để tìm thấy), gom
        // vào 1 node "đơn vị" tạm ở gốc cây để vẫn duyệt/xem/chọn được, kèm gợi ý cho admin đi sửa mapping.
        // Cùng kỹ thuật GROUP BY + JOIN ở trên, chỉ đổi điều kiện UNIT_ID IS NULL — không phải correlated.
        // CHỈ hiện với quản trị hệ thống (allowedIdsList null) — các bản ghi này chưa thuộc đơn vị nào nên
        // người dùng thường không có "đơn vị" nào để được coi là chủ sở hữu.
        var hasUnassigned = allowedIdsList == null && await _connection.ExecuteScalarAsync<int>($@"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM INFRASTRUCTURE i
                LEFT JOIN (
                    SELECT OwnerId
                    FROM PMIS_DOCUMENT
                    WHERE OwnerType = 'INFRASTRUCTURE' AND IsDeleted = 0
                    GROUP BY OwnerId
                ) direct_doc ON direct_doc.OwnerId = i.Id
                LEFT JOIN (
                    SELECT e.INFRASTRUCTURE_ID AS InfrastructureId
                    FROM EQUIPMENTS e
                    INNER JOIN PMIS_DOCUMENT pd ON pd.OwnerType = 'EQUIPMENT' AND pd.OwnerId = e.Id AND pd.IsDeleted = 0
                    WHERE e.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("e")}
                    GROUP BY e.INFRASTRUCTURE_ID
                ) child_doc ON child_doc.InfrastructureId = i.Id
                WHERE i.UNIT_ID IS NULL AND i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
                  AND (direct_doc.OwnerId IS NOT NULL OR child_doc.InfrastructureId IS NOT NULL)
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
        var infraRows = (await _connection.QueryAsync<InfraCatalogRow>($@"
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
                WHERE e.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("e")}
                GROUP BY e.INFRASTRUCTURE_ID
            ) child_doc ON child_doc.InfrastructureId = i.Id
            WHERE i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
              AND (direct_doc.DocCount IS NOT NULL OR child_doc.DocCount IS NOT NULL)
              AND ((:UnitId IS NULL AND i.UNIT_ID IS NULL) OR i.UNIT_ID = :UnitId)",
            new { UnitId = unitId }))
            .ToList();

        var nodes = new List<PmisDocumentCatalogNodeDto>();
        if (infraRows.Count == 0) return nodes;

        // {EquipmentSqlFilters.NotTransferredAway()} — đây chính là các dòng EQUIPMENT sẽ hiện làm node
        // con bấm được trong cây "Kho tài liệu PMIS", quan trọng nhất phải loại "hồn ma" (xem GetPagedAsync).
        var infraIds = infraRows.Select(r => r.Id).ToList();
        var equipmentRows = (await _connection.QueryAsync<EquipmentCatalogRow>($@"
            SELECT e.Id, e.Name, e.Code, e.INFRASTRUCTURE_ID AS InfrastructureId, doc.DocCount AS DocumentCount
            FROM EQUIPMENTS e
            INNER JOIN (
                SELECT OwnerId, COUNT(1) AS DocCount
                FROM PMIS_DOCUMENT
                WHERE OwnerType = 'EQUIPMENT' AND IsDeleted = 0
                GROUP BY OwnerId
            ) doc ON doc.OwnerId = e.Id
            WHERE e.PMIS_CODE IS NOT NULL AND e.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("e")}
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
            $@"SELECT Id, PmisDocumentCode, OwnerType, OwnerId, DocumentName, DocumentType, ObjectKey, FileSize, SyncedAt, CreatedBy,
                     FILE_STATUS AS FileStatus, FILE_LAST_ERROR AS FileLastError
               FROM PMIS_DOCUMENT
               {whereSql}
               ORDER BY SyncedAt DESC
               OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY", parameters);

        return (rows.Select(ToDetail), totalCount);
    }

    public async Task<long?> GetOwnerUnitIdAsync(string ownerType, Guid ownerId)
    {
        EnsureOpen();

        var sql = ownerType switch
        {
            "INFRASTRUCTURE" => "SELECT UNIT_ID FROM INFRASTRUCTURE WHERE Id = :OwnerId AND IsDeleted = 0",
            "EQUIPMENT" => "SELECT UnitId FROM EQUIPMENTS WHERE Id = :OwnerId AND IsDeleted = 0",
            _ => null
        };
        if (sql == null) return null;

        return await _connection.QuerySingleOrDefaultAsync<long?>(sql, new { OwnerId = ownerId.ToString() });
    }

    public async Task<IReadOnlyList<PmisInfrastructureLookupDto>> SearchInfrastructuresAsync(IEnumerable<long>? allowedUnitIds)
    {
        EnsureOpen();

        var allowedIdsList = allowedUnitIds?.ToList();

        var sql = $@"
            SELECT i.Id, i.Name, i.Code, i.INFRA_TYPE_ID AS InfraTypeId, i.UNIT_ID AS UnitId, ou.Name AS UnitName,
                   NVL(direct_doc.DocCount, 0) AS DirectDocumentCount
            FROM INFRASTRUCTURE i
            LEFT JOIN ORGANIZATION_UNIT ou ON ou.Id = i.UNIT_ID
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
                WHERE e.IsDeleted = 0 AND {EquipmentSqlFilters.NotTransferredAway("e")}
                GROUP BY e.INFRASTRUCTURE_ID
            ) child_doc ON child_doc.InfrastructureId = i.Id
            WHERE i.PMIS_CODE IS NOT NULL AND i.IsDeleted = 0
              AND (direct_doc.DocCount IS NOT NULL OR child_doc.DocCount IS NOT NULL)";

        var parameters = new DynamicParameters();
        if (allowedIdsList != null)
        {
            // Không phải quản trị hệ thống: chỉ thấy Trạm/Đường dây thuộc đơn vị được phép, loại luôn các
            // bản ghi chưa xác định đơn vị (UNIT_ID NULL sẽ không khớp IN nên tự động bị loại).
            sql += " AND i.UNIT_ID IN :AllowedUnitIds";
            parameters.Add("AllowedUnitIds", allowedIdsList.Count > 0 ? allowedIdsList : new List<long> { -1 });
        }

        sql += " ORDER BY i.Name";

        var rows = await _connection.QueryAsync<InfraLookupRow>(sql, parameters);

        return rows.Select(r => new PmisInfrastructureLookupDto
        {
            Id = $"infra_{r.Id}",
            Name = string.IsNullOrEmpty(r.Code) ? r.Name : $"{r.Name} ({r.Code})",
            Code = r.Code,
            NodeType = r.InfraTypeId == 1 ? "substation" : "line",
            DocumentCount = r.DirectDocumentCount,
            UnitNodeId = r.UnitId.HasValue ? $"unit_{r.UnitId}" : UnassignedUnitNodeId,
            UnitName = r.UnitName ?? "(Chưa xác định đơn vị)"
        }).ToList();
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
        IsManual = !string.Equals(row.CreatedBy, "PMIS_SYNC", StringComparison.OrdinalIgnoreCase),
        FileStatus = row.FileStatus,
        FileLastError = row.FileLastError
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
        public string FileStatus { get; set; } = "NO_URL";
        public string? FileLastError { get; set; }
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

    private class InfraLookupRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int InfraTypeId { get; set; }
        public long? UnitId { get; set; }
        public string? UnitName { get; set; }
        public int DirectDocumentCount { get; set; }
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
