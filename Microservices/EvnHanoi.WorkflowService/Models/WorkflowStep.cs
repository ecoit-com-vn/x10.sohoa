using System;
using System.Text.Json.Serialization;

namespace EvnHanoi.WorkflowService.Models
{
    public class WorkflowStep
    {
        public Guid Id { get; set; }
        public Guid WorkflowDefinitionId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int Order { get; set; }
        public string RequiredRole { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // e.g., "Scan", "DataEntry", "Approve"
        public bool AllowEdit { get; set; }
        public bool RequireSignature { get; set; }

        /// <summary>Danh sách ID nhóm quyền hệ thống được phép xử lý (phân cách bởi dấu phẩy).</summary>
        public string? SystemPermissionGroupIds { get; set; }

        /// <summary>Danh sách ID nhóm quyền đơn vị được phép xử lý (phân cách bởi dấu phẩy).</summary>
        public string? UnitPermissionGroupIds { get; set; }

        /// <summary>Bắt buộc người xử lý tiếp theo phải cùng đơn vị với người đang chuyển bước.</summary>
        public bool RequireSameUnit { get; set; }

        /// <summary>ID "Người cụ thể" — 1 ID hoặc danh sách nhiều ID (phân cách bởi dấu phẩy) nếu bước cấu hình nhiều người.</summary>
        public string? AssigneeId { get; set; }

        [JsonIgnore]
        public WorkflowDefinition? WorkflowDefinition { get; set; }
    }
}
