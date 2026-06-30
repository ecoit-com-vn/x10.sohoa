namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Hồ sơ thiết bị - thiết kế lại với workflow integration và EAV form data
/// </summary>
public class Dossier
{
    public Guid Id { get; set; }
    public int? GridTypeId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public Guid? DossierSetId { get; set; }
    public Guid DossierTypeId { get; set; }
    public string? FormDataJson { get; set; }
    public int StatusId { get; set; } = DossierStatusConstants.New;

    // Workflow integration
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatusName { get; set; }

    // Optimistic locking
    public int RowVersion { get; set; } = 1;

    // Creator info (denormalized for display performance)
    public Guid? CreatorId { get; set; }
    public string? CreatorUsername { get; set; }
    public string? CreatorName { get; set; }

    // Audit fields (theo chuẩn BACKEND_GUIDELINES)
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Publish status
    public int? PublishStatusId { get; set; }
}

public static class DossierStatusConstants
{
    public const int New = 1;
    public const int CompletedInput = 2;
    public const int PendingApproval = 3;
    public const int InProgress = 4;
    public const int Returned = 5;
    public const int Approved = 6;
}

public static class DossierPublishStatusConstants
{
    public const int Pending = 1;
    public const int Published = 2;
    public const int Unpublished = 3;
}
