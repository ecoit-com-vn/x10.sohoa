namespace EvnHanoi.SyncService.Models.Internal;

// Bản sao (mirror) đúng shape DTO nội bộ của EquipmentService (Core/DTOs/PmisSyncDtos.cs) —
// mỗi service tự giữ 1 bản hợp đồng, không tham chiếu project chéo giữa 2 microservice.

public class UpsertInfrastructureFromPmisRequest
{
    public int InfraTypeId { get; set; }
    public string PmisCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? UnitCode { get; set; }
    public DateTime? OperationDate { get; set; }
    public int? GridTypeId { get; set; }

    /// <summary>Chỉ có ý nghĩa với Đường dây (InfraTypeId=2): mã PMIS của đường dây CHA (field "maCha",
    /// PMIS bổ sung 2026-09-23) — null/rỗng nghĩa là đường trục gốc, không có cha. EquipmentService tự tra
    /// Id theo mã này (UPPER(TRIM(PMIS_CODE))) khi lưu, giống hệt cách ParentPmisCode được dùng cho Thiết
    /// bị (UpsertEquipmentFromPmisRequest) — không cần SyncService tự tra sẵn Guid như trước (xem
    /// InfrastructureRepository.UpsertFromPmisAsync). Trạm biến áp luôn để null.</summary>
    public string? ParentPmisCode { get; set; }
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
    /// (đường trục chưa đồng bộ tới, mã sai, hoặc tự trỏ về chính dòng này) — chỉ có ý nghĩa với Đường dây
    /// (xem PmisSyncExecutionService.SyncInfrastructureAsync, ghi 1 dòng Warning trong "Lịch sử đồng bộ").</summary>
    public bool ParentUnresolved { get; set; }
}

public class UpsertEquipmentFromPmisRequest
{
    public string PmisCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string EquipmentTypeCode { get; set; } = string.Empty;
    public string? EquipmentTypeName { get; set; }
    public string? ParentPmisCode { get; set; }
    public string? UnitCode { get; set; }
    public int? ManufactureYear { get; set; }
    public string? QrCodeBase64 { get; set; }
    public int? GridTypeId { get; set; }
    public string? ThongSoKyThuat { get; set; }

    /// <summary>Nhãn tiếng Việt cho từng khoá của ThongSoKyThuat (PMIS bổ sung 2026-09-23) — lưu riêng
    /// vào EQUIPMENT_PMIS_SPEC.FieldLabels.</summary>
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

public class UpsertPmisDocumentRequest
{
    public string PmisDocumentCode { get; set; } = string.Empty; // MaTaiLieu
    public string OwnerType { get; set; } = string.Empty;        // INFRASTRUCTURE | EQUIPMENT
    public string OwnerPmisCode { get; set; } = string.Empty;    // MaTBA/MaDuongDay/MaTB — dò OwnerId phía server
    public string? DocumentName { get; set; }
    public string? DocumentType { get; set; }
    public string? FileName { get; set; }
    public string? FileBase64 { get; set; }                     // null nếu SyncService tải file thất bại
    public string? SyncHistoryId { get; set; }

    /// <summary>Nguyên nhân THẬT khi FileBase64=null (vd "HttpRequestException: Response status code does
    /// not indicate success: 404 (Not Found).") — rút gọn qua SyncErrorFormatter, không lộ stack trace.
    /// Trước đây lỗi này chỉ có trong log Serilog của pod SyncService, EquipmentService chỉ biết "file
    /// rỗng" mà không biết vì sao — server ghép chuỗi này vào ErrorMessage trả về để hiện luôn trong màn
    /// "Lịch sử đồng bộ", phục vụ debug mà không cần vào log pod.</summary>
    public string? FileDownloadError { get; set; }

    /// <summary>Mã thiết bị PMIS (maTB) đính kèm trên chính dòng tài liệu này, nếu có — server ưu tiên
    /// gán OwnerType=EQUIPMENT theo mã này khi thiết bị đã tồn tại (xem EquipmentService.InternalPmisSyncController).</summary>
    public string? DeviceCode { get; set; }
}

public class UpsertPmisDocumentResult
{
    public string PmisDocumentCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool WasSkippedAsExisting { get; set; }
    public string? ErrorMessage { get; set; }
}
