using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.DTOs;

// ===== DOSSIER SET DTOs =====

public class DossierSetDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class DossierSetCreateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? UnitId { get; set; }
}

public class DossierSetUpdateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? UnitId { get; set; }
}

// ===== DOSSIER DTOs =====

/// <summary>
/// DTO dùng cho danh sách hồ sơ — bao gồm catalog columns loại BHS + trạm/đường dây
/// </summary>
public class DossierListItemDto
{
    public Guid Id { get; set; }
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public Guid? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public Guid? DossierSetId { get; set; }
    public string? DossierSetName { get; set; }
    public Guid DossierTypeId { get; set; }
    public string? DossierTypeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? WorkflowStatusName { get; set; }
    public int DocumentCount { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; }
    /// <summary>
    /// Dữ liệu catalog động theo BHS — key = catalog.Name (trùng key FormDataJson), value = giá trị từ JSON
    /// </summary>
    public Dictionary<string, string> CatalogData { get; set; } = new();
}

/// <summary>
/// DTO chi tiết hồ sơ — bao gồm FormDataJson và danh sách thiết bị
/// </summary>
public class DossierDetailDto
{
    public Guid Id { get; set; }
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public Guid? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public Guid? DossierSetId { get; set; }
    public string? DossierSetName { get; set; }
    public Guid DossierTypeId { get; set; }
    public string? DossierTypeName { get; set; }
    /// <summary>Form EAV gắn với loại hồ sơ — dùng gen trường động không cần gọi lookup.</summary>
    public Guid? FormId { get; set; }
    public string? FormDataJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatusName { get; set; }
    public int RowVersion { get; set; }
    public CreatorInfoDto? Creator { get; set; }
    public List<DossierEquipmentDto> Equipments { get; set; } = new();
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

/// <summary>
/// DTO tạo mới hồ sơ — bao gồm cả FormDataJson từ biểu mẫu động EAV
/// </summary>
public class DossierCreateDto
{
    public int? GridTypeId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public Guid? DossierSetId { get; set; }
    public Guid DossierTypeId { get; set; }
    public List<Guid> EquipmentIds { get; set; } = new();
    /// <summary>Dữ liệu form động (JSON) từ EAV template, lưu cùng lúc với tạo hồ sơ.</summary>
    public string? FormDataJson { get; set; }
}

/// <summary>
/// DTO chỉnh sửa hồ sơ — bao gồm cả FormDataJson từ biểu mẫu động EAV
/// </summary>
public class DossierUpdateDto
{
    public int? GridTypeId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public Guid? DossierSetId { get; set; }
    public Guid DossierTypeId { get; set; }
    public List<Guid> EquipmentIds { get; set; } = new();
    public int RowVersion { get; set; }
    /// <summary>Dữ liệu form động (JSON) từ EAV template, lưu cùng lúc với cập nhật hồ sơ.</summary>
    public string? FormDataJson { get; set; }
}

/// <summary>
/// DTO lưu FormDataJson (Tab thông tin chi tiết hồ sơ)
/// </summary>
public class DossierSaveFormDataDto
{
    public string FormDataJson { get; set; } = string.Empty;
    public string? ChangeNote { get; set; }
    public int RowVersion { get; set; }
}

// ===== EQUIPMENT IN DOSSIER =====

public class DossierEquipmentDto
{
    public Guid EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public string? SerialNumber { get; set; }
    public string? EquipmentTypeName { get; set; }
    public string? InfrastructureName { get; set; }
}

public class AddDossierEquipmentDto
{
    public Guid EquipmentId { get; set; }
}

/// <summary>
/// Thiết bị lookup cho popup gắn thiết bị vào hồ sơ — chỉ trường hiển thị cơ bản.
/// </summary>
public class EquipmentLookupItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? InfrastructureName { get; set; }
}

/// <summary>
/// Bộ lọc lookup thiết bị cho màn tạo/sửa hồ sơ.
/// </summary>
public class EquipmentLookupFilterDto
{
    /// <summary>Tìm gộp theo mã hoặc tên thiết bị.</summary>
    public string? Keyword { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public Guid? InfrastructureId { get; set; }
    public int? GridTypeId { get; set; }
    public long? UnitId { get; set; }
    public bool? IsActive { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// ===== DOSSIER VERSION =====

public class DossierVersionDto
{
    public Guid Id { get; set; }
    public Guid DossierId { get; set; }
    public int VersionNumber { get; set; }
    public string? FormDataJson { get; set; }
    public string? DocumentsSnapshotJson { get; set; }
    public string? ChangeNote { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

// ===== FILTER / QUERY =====

public class DossierFilterDto
{
    public string? Keyword { get; set; }
    public Guid? InfrastructureId { get; set; }
    public int? GridTypeId { get; set; }
    public long? UnitId { get; set; }
    /// <summary>Đơn vị + đơn vị con — do service resolve từ UnitId trước khi query ES.</summary>
    public IReadOnlyList<long>? UnitScopeIds { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class DossierWorkflowStatusDto
{
    public Guid InstanceId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string? CurrentNodeId { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentStepOrder { get; set; }
    public string CurrentStepName { get; set; } = string.Empty;
    public bool CurrentStepAllowEdit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DossierWorkflowPendingTaskDto> PendingTasks { get; set; } = new();
}

public class DossierWorkflowPendingTaskDto
{
    public Guid Id { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string AssignedRole { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public bool AllowEdit { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Payload đồng bộ trạng thái workflow từ WorkflowService về EquipmentService (API nội bộ).
/// WorkflowService tự suy ra DossierStatus rồi đẩy về đây.
/// </summary>
public class UpdateInternalWorkflowStateDto
{
    /// <summary>Id của WorkflowInstance vừa thay đổi.</summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>Tên trạng thái/bước hiển thị (gán vào Dossier.WorkflowStatusName).</summary>
    public string? WorkflowStatusName { get; set; }

    /// <summary>Trạng thái nghiệp vụ do WS suy ra: Draft | PendingApproval | InProgress | Returned | Approved.</summary>
    public string DossierStatus { get; set; } = string.Empty;
}
