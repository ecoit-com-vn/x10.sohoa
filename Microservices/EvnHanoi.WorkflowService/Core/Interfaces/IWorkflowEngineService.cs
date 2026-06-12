using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IWorkflowEngineService
    {
        Task<WorkflowInstance> SubmitAsync(Guid definitionId, string targetEntityId, string targetEntityType, string userId);
        Task<WorkflowTask> ApproveAsync(Guid taskId, string userId, string? comment = null);
        Task<WorkflowTask?> RejectAsync(Guid taskId, string userId, string? comment = null);
        Task<WorkflowInstance> MoveAsync(string targetEntityId, string nextNodeId, string userId, string actionLabel, string? comment = null);
        Task<WorkflowInstance> MoveWithValidationAsync(string targetEntityId, string nextNodeId, string userId, List<string> userRoles, bool isAdmin, string actionLabel, string? comment = null);
        Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin);
        Task<IEnumerable<WorkflowHistory>> GetHistoryAsync(Guid instanceId);
        Task<object> GetInstanceStatusByEntityAsync(string entityId, string entityType);
    }
}
