using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IWorkflowEngineService
    {
        Task<WorkflowInstance> SubmitAsync(Guid definitionId, string targetEntityId, string targetEntityType, string userId);

        /// <summary>
        /// Tự động tìm WorkflowDefinition theo <paramref name="entityType"/> (description của enum WorkflowType),
        /// nhưng gắn <paramref name="targetEntityType"/> vào WorkflowInstance.TargetEntityType
        /// để query sau (ví dụ: entityType = "Quy trình số hóa hồ sơ", targetEntityType = "Dossier").
        /// </summary>
        Task<WorkflowInstance> SubmitByEntityTypeAsync(string targetEntityId, string entityType, string targetEntityType, string userId);
        Task<WorkflowTask> ApproveAsync(Guid taskId, string userId, string? comment = null, string? nextAssigneeUserId = null);
        Task<WorkflowTask?> RejectAsync(Guid taskId, string userId, string? comment = null);
        Task<WorkflowInstance> MoveAsync(string targetEntityId, string nextNodeId, string userId, string actionLabel, string? comment = null, string? nextAssigneeUserId = null, string entityType = "BorrowRecord");
        Task<WorkflowInstance> MoveWithValidationAsync(string targetEntityId, string nextNodeId, string userId, List<string> userRoles, bool isAdmin, string actionLabel, string? comment = null, string? nextAssigneeUserId = null, string entityType = "BorrowRecord");
        Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId);
        Task<IEnumerable<WorkflowHistory>> GetHistoryAsync(Guid instanceId);
        Task<IEnumerable<WorkflowHistory>> GetHistoryByEntityAsync(string entityId, string entityType);
        Task<object> GetInstanceStatusByEntityAsync(string entityId, string entityType);
    }
}
