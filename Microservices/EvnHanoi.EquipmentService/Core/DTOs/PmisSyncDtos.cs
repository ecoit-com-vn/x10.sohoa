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
