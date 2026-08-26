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
}

public class UpsertInfrastructureFromPmisResult
{
    public string PmisCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public Guid? InfrastructureId { get; set; }
    public bool WasCreated { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UpsertEquipmentFromPmisRequest
{
    public string PmisCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string EquipmentTypeCode { get; set; } = string.Empty;
    public string? ParentPmisCode { get; set; }
    public string? UnitCode { get; set; }
    public int? ManufactureYear { get; set; }
    public string? QrCodeBase64 { get; set; }
    public int? GridTypeId { get; set; }
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
