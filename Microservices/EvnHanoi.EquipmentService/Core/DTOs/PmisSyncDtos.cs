namespace EvnHanoi.EquipmentService.Core.DTOs;

public class EquipmentPmisUpsertResult
{
    public bool Success { get; set; }
    public Guid? EquipmentId { get; set; }
    public bool WasCreated { get; set; }
    public string? ErrorMessage { get; set; }

    public static EquipmentPmisUpsertResult Ok(Guid id, bool wasCreated) =>
        new() { Success = true, EquipmentId = id, WasCreated = wasCreated };

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
    public string? ErrorMessage { get; set; }
}
