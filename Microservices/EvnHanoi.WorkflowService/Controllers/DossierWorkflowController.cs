namespace EvnHanoi.WorkflowService.Controllers;

/// <summary>Request chuyển bước workflow cho hồ sơ.</summary>
public class MoveDossierWorkflowRequest
{
    public string NextNodeId { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? NextAssigneeUserId { get; set; }
}

/// <summary>Request gửi duyệt và chuyển bước tích hợp.</summary>
public class SubmitAndMoveRequest
{
    public string NextNodeId { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = "Trình duyệt";
    public string? Comment { get; set; }
    public string? NextAssigneeUserId { get; set; }
}

/// <summary>DTO nhận trạng thái hồ sơ nghiệp vụ từ EquipmentService.</summary>
public class DossierDetailResponse
{
    public string Status { get; set; } = string.Empty;
    public int StatusId { get; set; }
}
