using EvnHanoi.WorkflowService.Models;

using System;

using System.Collections.Generic;

using System.Threading.Tasks;



namespace EvnHanoi.WorkflowService.Core.Interfaces

{

    public interface IWorkflowEngineService

    {

        Task<WorkflowInstance> SubmitByWorkflowTypeIdAsync(string entityId, int workflowTypeId, string userId);

        Task<WorkflowTask> ApproveAsync(Guid taskId, string userId, string? comment = null, string? nextAssigneeUserId = null);

        Task<WorkflowTask?> RejectAsync(Guid taskId, string userId, string? comment = null);

        Task<WorkflowInstance> MoveAsync(string entityId, string nextNodeId, string userId, string actionLabel, string? comment = null, string? nextAssigneeUserId = null, int workflowTypeId = 2);

        Task<WorkflowInstance> MoveWithValidationAsync(string entityId, string nextNodeId, string userId, List<string> userRoles, bool isAdmin, string actionLabel, string? comment = null, string? nextAssigneeUserId = null, int workflowTypeId = 2);

        Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId, Guid? workflowInstanceId = null);

        Task<IEnumerable<WorkflowHistory>> GetHistoryAsync(Guid instanceId);

        Task<IEnumerable<WorkflowHistory>> GetHistoryByEntityAsync(string entityId, int workflowTypeId);

        Task<object> GetInstanceStatusByEntityAsync(string entityId, int workflowTypeId);

    }

}


