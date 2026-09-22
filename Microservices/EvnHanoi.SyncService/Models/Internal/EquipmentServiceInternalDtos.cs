using System.Text.Json.Serialization;

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

    /// <summary>Chỉ có ý nghĩa với Đường dây (InfraTypeId=2): true nếu tên KHÔNG có "/" (đường trục gốc,
    /// chắc chắn không có cha). Trạm biến áp luôn để false.</summary>
    public bool IsRootLine { get; set; }

    /// <summary>Id đường dây CHA đã tự tra sẵn qua danh mục tải 1 lần/lượt đồng bộ (xem
    /// PmisSyncExecutionService.ResolveParentLineIdAsync) — null nếu IsRootLine=true, hoặc có "/" nhưng
    /// chưa/không xác định được cha (giữ nguyên PARENT_ID cũ phía EquipmentService, không xoá).</summary>
    public Guid? ParentInfrastructureId { get; set; }
}

/// <summary>Mirror của LineNameIndexEntry (EquipmentService) — 1 dòng danh mục Đường dây hiện có, tải 1
/// lần/lượt đồng bộ để tự tìm cha theo tên trong bộ nhớ (xem PmisSyncExecutionService).</summary>
public class LineNameIndexEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PmisUnitCode { get; set; }

    /// <summary>GRIDTYPEID hiện có của chính dòng này — dùng để cho nhánh mượn tạm cấp điện áp của trục
    /// khi nhánh không có capDienAp riêng (xem PmisSyncExecutionService.ResolveParentLineId).</summary>
    public int? GridTypeId { get; set; }

    /// <summary>Chỉ có giá trị khi dòng này được trả về bởi GetLinesNeedingBackfillAsync VÀ đã có cha —
    /// cho biết ngay ParentId THẬT (không cần resolve lại theo tên) và GridTypeId hiện có của cha đó
    /// (ParentGridTypeId), để BackfillLineParentsAsync mượn thẳng khi chính dòng này thiếu GridTypeId.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Xem ParentId — GRIDTYPEID hiện có của dòng cha (null nếu cha cũng chưa có).</summary>
    public int? ParentGridTypeId { get; set; }
}

/// <summary>Mirror của BackfillLineParentRequest/Item (EquipmentService) — cập nhật RIÊNG cột
/// ParentInfrastructureId cho các Đường dây ĐÃ tồn tại nhưng chưa xác định được cha ở lượt trước (xem
/// PmisSyncExecutionService.BackfillLineParentsAsync).</summary>
public class BackfillLineParentRequest
{
    public List<BackfillLineParentItem> Items { get; set; } = [];
}

public class BackfillLineParentItem
{
    public Guid Id { get; set; }
    public Guid ParentInfrastructureId { get; set; }

    /// <summary>Cấp lưới điện mượn tạm từ đường trục cha, CHỈ khi dòng này đang GRIDTYPEID=NULL — null có
    /// nghĩa là không cần cập nhật cột này.</summary>
    public int? GridTypeId { get; set; }
}

public class BackfillLineParentResult
{
    // Server trả về { updatedCount } (anonymous object, camelCase) — gắn tên tường minh thay vì phụ
    // thuộc naming policy mặc định của JsonSerializerOptions (khác các DTO khác trong file này vốn là
    // class thật ở cả 2 phía nên khớp tên tự nhiên).
    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; set; }
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
