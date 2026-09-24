namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>Các cột EQUIPMENTS cần để EquipmentRepository.UpsertFromPmisAsync so sánh "có gì thay đổi
/// thật không" trước khi UPDATE — dùng cả ở đường tra 1-dòng cũ (không set NormalizedPmisCode) lẫn
/// EquipmentUpsertPrefetch.ExistingByPmisCode (batch, có set NormalizedPmisCode làm key).</summary>
public class EquipmentCompareRow
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? SerialNumber { get; set; }
    public string? InfrastructureId { get; set; }
    public int? ManufactureYear { get; set; }
    public long? UnitId { get; set; }
    public string? EquipmentTypeId { get; set; }
    public long? EquipmentStatusId { get; set; }
    public string? FormValues { get; set; }

    /// <summary>UPPER(TRIM(PMIS_CODE)) — chỉ dùng làm KEY khi trả về theo lô trong
    /// EquipmentUpsertPrefetch.ExistingByPmisCode, không dùng ở đường tra 1-dòng cũ.</summary>
    public string? NormalizedPmisCode { get; set; }
}

/// <summary>Kết quả prefetch theo LÔ (1 trang PMIS) cho 4 lookup lặp lại per-item trong
/// EquipmentRepository.UpsertFromPmisAsync — xem EquipmentRepository.PrefetchUpsertLookupsAsync. Truyền
/// vào UpsertFromPmisAsync qua tham số <c>prefetch</c> để thay 4 round-trip DB/bản ghi bằng tra dictionary
/// trong bộ nhớ; truyền null (mặc định) giữ nguyên hành vi cũ (tự SELECT riêng từng lookup) — dùng cho
/// các caller xử lý 1 item lẻ (PmisManualSyncController.RefreshEquipment, hoặc bất kỳ nơi nào gọi trực
/// tiếp không qua batch).</summary>
public class EquipmentUpsertPrefetch
{
    /// <summary>Key: UPPER(TRIM(parentPmisCode)).</summary>
    public Dictionary<string, (string Id, int? GridTypeId)> ParentByPmisCode { get; init; } = new();
    /// <summary>Key: (PmisMaLoaiTB nguyên văn, GridTypeId) — PMIS_EQUIPMENT_TYPE_MAPPING là bảng nhỏ
    /// (&lt;200 dòng), fetch TOÀN BỘ 1 lần rẻ hơn hẳn lọc theo composite key.</summary>
    public Dictionary<(string Code, int GridTypeId), string> EquipmentTypeByCodeAndGrid { get; init; } = new();
    /// <summary>Key: PmisUnitCode nguyên văn — PMIS_UNIT_CODE_MAPPING cũng bảng nhỏ, fetch toàn bộ.</summary>
    public Dictionary<string, long> UnitIdByPmisCode { get; init; } = new();
    /// <summary>Key: UPPER(TRIM(pmisCode)) của CHÍNH thiết bị.</summary>
    public Dictionary<string, EquipmentCompareRow> ExistingByPmisCode { get; init; } = new();
}

public class EquipmentPmisUpsertResult
{
    public bool Success { get; set; }
    public Guid? EquipmentId { get; set; }
    public bool WasCreated { get; set; }

    /// <summary>false nếu bản ghi đã tồn tại và dữ liệu PMIS gửi về giống hệt dữ liệu đang lưu — không
    /// issue câu UPDATE, caller ghi ACTION_TYPE=SKIP thay vì UPDATE.</summary>
    public bool HasChanged { get; set; } = true;
    public string? ErrorMessage { get; set; }

    /// <summary>Loại thiết bị đã tra/tự tạo (xem EquipmentRepository.ResolveOrCreateEquipmentTypeIdAsync)
    /// — dùng ở tầng controller để tự tạo biểu mẫu thông số kỹ thuật nếu loại thiết bị chưa có.</summary>
    public Guid? EquipmentTypeId { get; set; }

    /// <summary>true nếu PMIS báo thiết bị đã đổi Trạm/Đường dây — <see cref="EquipmentId"/> ở trên là bản
    /// ghi MỚI vừa tạo (không phải bản ghi đang đồng bộ), bản ghi cũ (<see cref="OldEquipmentId"/>) đã bị
    /// đánh dấu StatusTransition=0 "Đã chuyển TBA". Dùng ở tầng controller để phát sự kiện thông báo.</summary>
    public bool WasTransferred { get; set; }
    public Guid? OldEquipmentId { get; set; }
    public long? OldUnitId { get; set; }
    public long? NewUnitId { get; set; }

    public static EquipmentPmisUpsertResult Ok(Guid id, bool wasCreated, bool hasChanged, Guid equipmentTypeId) =>
        new() { Success = true, EquipmentId = id, WasCreated = wasCreated, HasChanged = hasChanged, EquipmentTypeId = equipmentTypeId };

    public static EquipmentPmisUpsertResult Transferred(Guid newId, Guid equipmentTypeId, Guid oldEquipmentId, long? oldUnitId, long? newUnitId) =>
        new()
        {
            Success = true, EquipmentId = newId, WasCreated = true, HasChanged = true, EquipmentTypeId = equipmentTypeId,
            WasTransferred = true, OldEquipmentId = oldEquipmentId, OldUnitId = oldUnitId, NewUnitId = newUnitId
        };

    public static EquipmentPmisUpsertResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

/// <summary>Payload endpoint nội bộ POST /api/v1/infrastructure/internal/upsert-from-pmis (gọi bởi SyncService).</summary>
public class UpsertInfrastructureFromPmisRequest
{
    public int InfraTypeId { get; set; } // 1 = Trạm biến áp, 2 = Đường dây
    public string PmisCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? UnitCode { get; set; } // maDonVi
    public DateTime? OperationDate { get; set; }
    public int? GridTypeId { get; set; } // Suy ra từ capDienAp (1 = Cao áp, 2 = Trung áp, 3 = Hạ áp) — xem PmisSyncExecutionService.ResolveGridTypeId

    /// <summary>Chỉ có ý nghĩa với Đường dây (InfraTypeId=2): mã PMIS của đường dây CHA (field "maCha",
    /// PMIS bổ sung 2026-09-23) — null/rỗng nghĩa là đường trục gốc (PARENT_ID phải xoá hẳn nếu trước đó
    /// có). Server tự SELECT INFRASTRUCTURE theo PMIS_CODE này để lấy Id + GridTypeId (mượn tạm cho nhánh
    /// không có capDienAp riêng) — xem InfrastructureRepository.UpsertFromPmisAsync. Không tìm thấy (đường
    /// trục chưa đồng bộ tới) → giữ nguyên PARENT_ID cũ, không xoá, tự khớp đúng ở lượt đồng bộ kế tiếp
    /// (PMIS trả toàn bộ dữ liệu mỗi lượt, không phải delta). Trạm biến áp luôn để null.</summary>
    public string? ParentPmisCode { get; set; }

    /// <summary>Mã hệ thống CMIS (field "maCMIS", PMIS bổ sung 2026-09-24) — khác PMIS_CODE, chỉ lưu tham
    /// khảo/hiển thị, không dùng làm khoá tra cứu.</summary>
    public string? CmisCode { get; set; }
}

public class UpsertInfrastructureFromPmisResult
{
    public string PmisCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public Guid? InfrastructureId { get; set; }
    public bool WasCreated { get; set; }
    public bool HasChanged { get; set; } = true;
    public string? ErrorMessage { get; set; }

    /// <summary>true khi ParentPmisCode ("maCha") CÓ giá trị nhưng KHÔNG khớp được INFRASTRUCTURE nào
    /// (đường trục chưa đồng bộ tới, mã sai, hoặc tự trỏ về chính dòng này) — chỉ có ý nghĩa với Đường dây.
    /// SyncService dùng để ghi 1 dòng Warning trong "Lịch sử đồng bộ" (xem
    /// InfrastructureRepository.UpsertFromPmisAsync).</summary>
    public bool ParentUnresolved { get; set; }
}

/// <summary>Payload endpoint nội bộ POST /api/v1/equipment/internal/upsert-from-pmis (gọi bởi SyncService).</summary>
public class UpsertEquipmentFromPmisRequest
{
    public string PmisCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string EquipmentTypeCode { get; set; } = string.Empty; // maLoaiTB
    public string? EquipmentTypeName { get; set; } // tenLoaiTB — dùng để đặt Name khi tự tạo EquipmentTypes mới
    public string? ParentPmisCode { get; set; } // maTBA hoặc maDuongDay
    public string? UnitCode { get; set; } // maDonVi
    public int? ManufactureYear { get; set; }
    public string? QrCodeBase64{ get; set; } // maQRCode

    /// <summary>Cấp điện áp (1 = Cao áp, 2 = Trung áp, 3 = Hạ áp) — thiết bị TBA có capDienAp riêng nên tự suy ra
    /// được, thiết bị đường dây thì để null (EquipmentRepository tự lấy từ GRIDTYPEID của đường dây cha).</summary>
    public int? GridTypeId { get; set; }

    /// <summary>Chuỗi JSON thông số kỹ thuật — lưu riêng vào EQUIPMENT_PMIS_SPEC, không ghi đè FormValues.</summary>
    public string? ThongSoKyThuat { get; set; }

    /// <summary>Chuỗi JSON nhãn tiếng Việt cho từng khoá của ThongSoKyThuat (PMIS bổ sung 2026-09-23,
    /// field "tenThongSoKyThuat") — lưu riêng vào EQUIPMENT_PMIS_SPEC.FieldLabels, dùng để gợi ý nhãn
    /// thật cho admin khi khai "Tên trường PMIS" trong Form Builder (xem
    /// EquipmentController.GetPmisSpecKeys).</summary>
    public string? TenThongSoKyThuat { get; set; }
}

public class UpsertEquipmentFromPmisResult
{
    public string PmisCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public Guid? EquipmentId { get; set; }
    public bool WasCreated { get; set; }
    public bool HasChanged { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>Payload endpoint nội bộ POST internal/v1/documents/upsert-from-pmis (gọi bởi SyncService).</summary>
public class UpsertPmisDocumentRequest
{
    public string PmisDocumentCode { get; set; } = string.Empty; // maTaiLieu
    public string OwnerType { get; set; } = string.Empty;        // INFRASTRUCTURE | EQUIPMENT
    public string OwnerPmisCode { get; set; } = string.Empty;    // maTBA/maDuongDay/maTB — dò OwnerId phía server
    public string? DocumentName { get; set; }
    public string? DocumentType { get; set; }
    public string? FileName { get; set; }
    public string? FileBase64 { get; set; }                     // null nếu SyncService tải file thất bại
    public string? SyncHistoryId { get; set; }

    /// <summary>Nguyên nhân THẬT khi FileBase64=null (vd "HttpRequestException: Response status code
    /// does not indicate success: 404 (Not Found).") — SyncService rút gọn qua SyncErrorFormatter trước
    /// khi gửi, không lộ stack trace. Ghép vào ErrorMessage trả về (xem InternalPmisSyncController) để
    /// hiện luôn trong màn "Lịch sử đồng bộ", phục vụ debug mà không cần vào log pod SyncService.</summary>
    public string? FileDownloadError { get; set; }

    /// <summary>Mã thiết bị PMIS (maTB) đính kèm trên CHÍNH dòng tài liệu này, nếu có — luôn gửi kèm dù
    /// gọi ở lượt đồng bộ Trạm/Đường dây hay Thiết bị. Khi có giá trị, server ưu tiên gán OwnerType=
    /// EQUIPMENT theo mã này (nếu thiết bị đã tồn tại) thay vì dùng OwnerType/OwnerPmisCode ở trên —
    /// tránh tài liệu vốn thuộc 1 thiết bị cụ thể bị gán nhầm cho Trạm/Đường dây cha khi lượt đồng bộ
    /// cấp Trạm/Đường dây (không lọc theo thiết bị) chạy trước lượt đồng bộ Thiết bị.</summary>
    public string? DeviceCode { get; set; }
}

public class UpsertPmisDocumentResult
{
    public string PmisDocumentCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool WasSkippedAsExisting { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Kết quả tra PMIS_DOCUMENT theo mã — ObjectKey null nghĩa là lần trước lưu được metadata
/// nhưng chưa tải được file (cần thử tải lại), khác với đã có file thật (bỏ qua hẳn).
/// OwnerType/OwnerId đi kèm để caller so sánh với chủ sở hữu vừa resolve lại được (xem
/// InternalPmisSyncController.UpsertDocumentsFromPmis) — tài liệu có thể đã bị gán nhầm cho
/// INFRASTRUCTURE ở lượt đồng bộ trước khi thiết bị thật tồn tại.</summary>
public class PmisDocumentLookup
{
    public string Id { get; set; } = string.Empty;
    public string? ObjectKey { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
}

/// <summary>1 dòng PMIS_DOCUMENT đầy đủ — dùng cho màn "Kho tài liệu PMIS" (đọc) và "Chọn từ kho PMIS"
/// (kiểm tra quyền/copy vào hồ sơ), khác PmisDocumentLookup (chỉ Id/ObjectKey, dùng lúc ghi/đồng bộ).</summary>
public class PmisDocumentDetail
{
    public Guid Id { get; set; }
    public string PmisDocumentCode { get; set; } = string.Empty;
    public string OwnerType { get; set; } = string.Empty; // INFRASTRUCTURE | EQUIPMENT
    public Guid OwnerId { get; set; }
    public string? DocumentName { get; set; }
    public string? DocumentType { get; set; }
    public string? ObjectKey { get; set; }
    public long? FileSize { get; set; }
    public DateTime SyncedAt { get; set; }

    /// <summary>true nếu do người dùng tự upload thủ công (nút "Upload tài liệu" khi đồng bộ tự động lỗi),
    /// false nếu đến từ đồng bộ PMIS thật — suy ra từ CreatedBy, không phải cột riêng.</summary>
    public bool IsManual { get; set; }
}

/// <summary>1 node cây "Kho tài liệu PMIS" (Đơn vị/Trạm biến áp/Đường dây/Thiết bị) — tổng hợp từ dữ
/// liệu thật (ORGANIZATION_UNIT/INFRASTRUCTURE/EQUIPMENTS + PMIS_DOCUMENT), KHÔNG có bảng folder riêng —
/// cùng khuôn với FolderCatalogNodeDto (DossierCatalogController) nhưng đơn giản hơn (3 cấp cố định,
/// không có cấp lưới điện cao/trung áp).</summary>
public class PmisDocumentCatalogNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }

    /// <summary>unit | substation | line | equipment — FE dùng để chọn icon và biết cấp nào cho phép bấm xem tài liệu trực tiếp.</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>Số tài liệu PMIS gắn TRỰC TIẾP vào node này (chỉ có ý nghĩa với substation/line/equipment — unit luôn 0).</summary>
    public int DocumentCount { get; set; }
}

/// <summary>1 dòng kết quả "Tìm Trạm/Đường dây" trên TOÀN BỘ công ty (không giới hạn theo 1 Đơn vị như
/// GetCatalogUnitChildrenAsync) — dùng cho ô tìm kiếm phía trên cây "Kho tài liệu PMIS", cho phép nhảy
/// thẳng tới đúng Trạm/Đường dây mà không cần biết nó thuộc công ty nào. Kèm UnitNodeId/UnitName để FE
/// tự tải + mở đúng nhánh cây chứa nó.</summary>
public class PmisInfrastructureLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    /// <summary>substation | line.</summary>
    public string NodeType { get; set; } = string.Empty;
    public int DocumentCount { get; set; }

    /// <summary>"unit_{id}" hoặc "unit_unassigned" - dùng để gọi GetCatalogUnitChildrenAsync và mở đúng
    /// công ty trong cây.</summary>
    public string UnitNodeId { get; set; } = string.Empty;
    public string? UnitName { get; set; }
}

/// <summary>1 dòng PMIS_UNIT_CODE_MAPPING (ánh xạ mã đơn vị PMIS ↔ UnitId thật) — xem Migration0051 và
/// PmisUnitCodeMappingController.</summary>
public class PmisUnitCodeMappingDto
{
    public Guid Id { get; set; }
    public string PmisUnitCode { get; set; } = string.Empty;
    public long UnitId { get; set; }
    public string? UnitName { get; set; }
    public string? UnitCode { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
}

/// <summary>Payload POST api/v1/pmis-unit-code-mapping — thêm 1 đơn vị PMIS chưa có ánh xạ.</summary>
public class CreatePmisUnitCodeMappingRequest
{
    public string PmisUnitCode { get; set; } = string.Empty;
    public long UnitId { get; set; }
    public string? Note { get; set; }
}

public enum PmisUnitCodeMappingCreateError
{
    None,

    /// <summary>PmisUnitCode đã có ánh xạ khác (UQ_PMIS_UNIT_CODE_MAPPING_CODE).</summary>
    DuplicateCode,

    /// <summary>UnitId gửi lên không khớp đơn vị thật nào (tránh để lộ lỗi FK Oracle thô ra response).</summary>
    UnitNotFound
}

public class CreatePmisUnitCodeMappingResult
{
    public Guid? Id { get; set; }
    public PmisUnitCodeMappingCreateError Error { get; set; } = PmisUnitCodeMappingCreateError.None;

    public static CreatePmisUnitCodeMappingResult Ok(Guid id) => new() { Id = id };
    public static CreatePmisUnitCodeMappingResult Fail(PmisUnitCodeMappingCreateError error) => new() { Error = error };
}
