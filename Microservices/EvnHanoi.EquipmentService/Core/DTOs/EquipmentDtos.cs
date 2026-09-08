// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.EquipmentService\Core\DTOs\EquipmentDtos.cs
using System;
using System.Collections.Generic;

namespace EvnHanoi.EquipmentService.Core.DTOs;

public class EquipmentCreateDto
{
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public int? ManufactureYear { get; set; }
    public long? EquipmentStatusId { get; set; }
    public bool IsActive { get; set; } = true;

    // Key: AttributeDefinitionId, Value: string
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
}

public class EquipmentUpdateDto
{
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public int? ManufactureYear { get; set; }
    public long? EquipmentStatusId { get; set; }
    public bool IsActive { get; set; }
    public string? FormValues { get; set; }
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
}

public class EquipmentDto
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string EquipmentTypeName { get; set; } = string.Empty;
    public string EquipmentTypeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? InfrastructureId { get; set; }
    public string InfrastructureName { get; set; } = string.Empty;
    public string InfrastructureCode { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public int? GridTypeId { get; set; }
    public string GridTypeName { get; set; } = string.Empty;
    public int? ManufactureYear { get; set; }
    public long? EquipmentStatusId { get; set; }
    public string EquipmentStatusName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? StatusTransition { get; set; }
    public CreatorInfoDto? Creator { get; set; }
    
    // Audit logs
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? FormValues { get; set; }
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
    public string? FormTemplateName { get; set; }
    public Guid? FormTemplateId { get; set; }
    public string? FormSchema { get; set; }

    /// <summary>
    /// Ảnh QR thiết bị dạng base64 (không kèm tiền tố "data:"), do PMIS cấp — đồng bộ về qua
    /// API AnhQRCode, xem PmisSyncExecutionService.SyncEquipmentAsync. CHỈ truy vấn chi tiết
    /// (GetDtoByIdAsync) mới trả về, danh sách phân trang để null để không phình payload (~58KB/thiết bị).
    /// </summary>
    public string? QrCode { get; set; }

    /// <summary>Mã PMIS của chính thiết bị này — null nếu thiết bị chưa từng đồng bộ từ PMIS.</summary>
    public string? PmisCode { get; set; }

    /// <summary>Mã PMIS của Trạm/Đường dây cha — cần để gọi API "Cập nhật thông số từ PMIS" cho 1 thiết bị.</summary>
    public string? ParentPmisCode { get; set; }

    /// <summary>1 = Trạm biến áp, 2 = Đường dây (INFRASTRUCTURE.INFRA_TYPE_ID của trạm/đường dây cha) —
    /// cần để "Cập nhật từ PMIS" gọi đúng API tài liệu đính kèm (SUBSTATION_DOCUMENT_LIST vs
    /// LINE_DOCUMENT_LIST), PMIS không tự phân biệt được qua ChiTietThietBi.</summary>
    public int? ParentInfraTypeId { get; set; }
}

public class EquipmentTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int GridTypeId { get; set; }
    public string GridTypeName { get; set; } = string.Empty;
    public int? SortOrder { get; set; }
    public bool IsActive { get; set; }
    public CreatorInfoDto? Creator { get; set; }
    
    // Audit logs
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
