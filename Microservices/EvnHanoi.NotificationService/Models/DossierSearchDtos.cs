namespace EvnHanoi.NotificationService.Models;

public static class DossierListTabs
{
    /// <summary>Slug tab UI/API — không ghi vào ES status.</summary>
    public const string Draft = "draft";
    /// <summary>Chờ xử lý (inbox) — ES status vẫn là PendingApproval/InProgress.</summary>
    public const string PendingAction = "pending-action";
    /// <summary>Đang xử lý (theo dõi pipeline) — ES status PendingApproval/InProgress.</summary>
    public const string InProgress = "in-progress";
    public const string Completed = "completed";
    public const string Returned = "returned";
    public const string PendingPublish = "pending-publish";
    public const string Published = "published";
    public const string Unpublished = "unpublished";
}

public class CreatorInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

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
    public int StatusId { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusName { get; set; }
    public string? WorkflowStepName { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    /// <summary>Trạng thái instance WF (Running/Completed…) — đọc từ ES, hỗ trợ debug tab.</summary>
    public string? WorkflowInstanceStatus { get; set; }
    public bool CurrentStepAllowEdit { get; set; }
    public int DocumentCount { get; set; }
    public CreatorInfoDto? Creator { get; set; }
    /// <summary>User được gán xử lý bước Pending hiện tại (inbox tab Chờ xử lý).</summary>
    public string? PendingAssigneeUserId { get; set; }
    public IReadOnlyList<string> PendingAssignedRoles { get; set; } = Array.Empty<string>();
    /// <summary>User đã tham gia WF — dùng tab Đang xử lý / Hoàn thành.</summary>
    public IReadOnlyList<string> WorkflowParticipantUserIds { get; set; } = Array.Empty<string>();
    public DateTime CreatedDate { get; set; }
    public string? CurrentStepId { get; set; }
    public IReadOnlyList<string> CurrentAssignees { get; set; } = Array.Empty<string>();
    public List<WorkflowActionEsDto> AvailableActions { get; set; } = new();
    public Dictionary<string, string> CatalogData { get; set; } = new();
}

public class DossierFilterDto
{
    public string? Keyword { get; set; }
    public Guid? InfrastructureId { get; set; }
    public int? GridTypeId { get; set; }
    public long? UnitId { get; set; }
    public IReadOnlyList<long>? UnitScopeIds { get; set; }
    /// <summary>Tab UI slug — KHÔNG phải giá trị ES status (Draft/InProgress/…).</summary>
    public string? Tab { get; set; }
    /// <summary>Filter trực tiếp field ES status nghiệp vụ — bỏ qua nếu có Tab.</summary>
    public int? StatusId { get; set; }
    public string? UserId { get; set; }
    public IReadOnlyList<string>? UserRoles { get; set; }
    public bool IsAdmin { get; set; }
    /// <summary>Phạm vi menu FE: creator | approver</summary>
    public string? MenuScope { get; set; }
    public Guid? DossierTypeId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class DossierTabCountsDto
{
    public int Draft { get; set; }
    public int PendingAction { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Returned { get; set; }
    public int PendingPublish { get; set; }
    public int Published { get; set; }
    public int Unpublished { get; set; }
}
