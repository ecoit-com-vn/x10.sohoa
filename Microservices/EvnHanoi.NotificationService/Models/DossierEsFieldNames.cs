namespace EvnHanoi.NotificationService.Models;

/// <summary>
/// Tên field trong index dossier_index — dùng literal khi build ES query (tránh lệch PascalCase/camelCase).
/// </summary>
public static class DossierEsFieldNames
{
    public const string Id = "id";
    public const string Status = "status";
    public const string IsDeleted = "isDeleted";
    public const string GridTypeId = "gridTypeId";
    public const string DossierTypeId = "dossierTypeId";
    public const string InfrastructureId = "infrastructureId";
    public const string UnitId = "unitId";
    public const string WorkflowInstanceId = "workflowInstanceId";
    public const string WorkflowInstanceStatus = "workflowInstanceStatus";
    public const string CreatorId = "creatorId";
    public const string PendingAssigneeUserId = "pendingAssigneeUserId";
    public const string PendingAssignedRoles = "pendingAssignedRoles";
    public const string WorkflowParticipantUserIds = "workflowParticipantUserIds";
    public const string CreatedDate = "createdDate";
    public const string CurrentStepOrder = "currentStepOrder";
    public const string WorkflowLastAction = "workflowLastAction";
    public const string IsReturnedToCreatorStep = "isReturnedToCreatorStep";
}
