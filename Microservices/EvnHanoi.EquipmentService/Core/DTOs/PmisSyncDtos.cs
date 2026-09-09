namespace EvnHanoi.EquipmentService.Core.DTOs;

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
}

public class UpsertInfrastructureFromPmisResult
{
    public string PmisCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public Guid? InfrastructureId { get; set; }
    public bool WasCreated { get; set; }
    public bool HasChanged { get; set; } = true;
    public string? ErrorMessage { get; set; }
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
/// nhưng chưa tải được file (cần thử tải lại), khác với đã có file thật (bỏ qua hẳn).</summary>
public class PmisDocumentLookup
{
    public string Id { get; set; } = string.Empty;
    public string? ObjectKey { get; set; }
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
