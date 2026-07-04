namespace EvnHanoi.NotificationService.Models;

public class DossierEnrichmentData
{
    public string Id { get; set; } = string.Empty;
    public int? GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public string? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    /// <summary>Tên trạm — nếu hồ sơ gắn đường dây thì suy từ thiết bị thuộc trạm.</summary>
    public string? StationName { get; set; }
    public long? UnitId { get; set; }
    public string? DossierSetId { get; set; }
    public string? DossierSetName { get; set; }
    public string DossierTypeId { get; set; } = string.Empty;
    public string? DossierTypeName { get; set; }
    public string? FormDataJson { get; set; }
    public int StatusId { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusName { get; set; }
    public string? WorkflowStatusName { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public string? WorkflowInstanceStatus { get; set; }
    public string? CreatorId { get; set; }
    public string? CreatorUsername { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int CurrentVersionNumber { get; set; }
    public bool IsDeleted { get; set; }
    public int DocumentCount { get; set; }
    public List<string> PendingAssignedRoles { get; set; } = new();
    public string? PendingAssigneeUserId { get; set; }
    /// <summary>Họ tên người xử lý hiện tại (pending assignee hoặc người tạo khi nháp).</summary>
    public string? CurrentHandlerName { get; set; }
    public List<string> WorkflowParticipantUserIds { get; set; } = new();
    public bool CurrentStepAllowEdit { get; set; }
    public string? CurrentStepId { get; set; }
    public int? CurrentStepOrder { get; set; }
    public string? WorkflowLastAction { get; set; }
    /// <summary>WF Running + hành động gần nhất Reject + đang ở bước đầu (người tạo).</summary>
    public bool IsReturnedToCreatorStep { get; set; }
    public string? CurrentAssignees { get; set; }
    public string? AvailableActionsJson { get; set; }

    public int? PublishStatusId { get; set; }
    public string? PublishStatusCode { get; set; }
    public string? PublishStatusName { get; set; }
    public int? KindId { get; set; }
    public string? KindCode { get; set; }
}

public class BhsCatalogDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class DossierEquipmentEnrichment
{
    public string EquipmentId { get; set; } = string.Empty;
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public string? SerialNumber { get; set; }
}
