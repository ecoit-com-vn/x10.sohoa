using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IWorkflowRepository
    {
        // Definitions & Steps
        Task<IEnumerable<WorkflowDefinition>> GetAllDefinitionsAsync(string? keyword, bool? isActive);
        Task<IEnumerable<WorkflowDefinition>> GetDefinitionsByWorkflowTypeIdAsync(int workflowTypeId);
        Task<bool> ExistsDefinitionByWorkflowTypeIdAsync(int workflowTypeId);
        Task<(IEnumerable<WorkflowDefinition> Items, int TotalCount)> GetPagedDefinitionsAsync(int page, int pageSize, string? keyword = null, bool? isActive = null);
        Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid id, bool includeBpmnXml = true);

        /// <summary>Lấy definition đang active theo WorkflowTypeId (1: Dossier, 2: BorrowRecord, 3: DossierDigitization).</summary>
        Task<WorkflowDefinition?> GetActiveDefinitionByWorkflowTypeIdAsync(int workflowTypeId);

        /// <summary>Kiểm tra nhanh instance đang Running — không load BPMN/tasks.</summary>
        Task<bool> ExistsRunningInstanceAsync(string entityId, int workflowTypeId);
        Task<WorkflowStep?> GetStepByIdAsync(Guid id);
        Task<bool> CreateDefinitionAsync(WorkflowDefinition definition);
        Task<bool> UpdateDefinitionAsync(Guid id, WorkflowDefinition definition);
        Task<bool> DeleteDefinitionAsync(Guid id);
        Task<bool?> ToggleDefinitionStatusAsync(Guid id);
        Task<bool> ReactivateDefinitionAsync(Guid id, int workflowTypeId, string name);
        
        // Instances
        Task<WorkflowInstance?> GetInstanceByEntityAsync(string entityId, int workflowTypeId, bool includeBpmnXml = true);
        Task<WorkflowInstance?> GetInstanceByIdAsync(Guid instanceId);
        Task<bool> CreateInstanceAsync(WorkflowInstance instance);
        Task<bool> UpdateInstanceAsync(WorkflowInstance instance);
        Task<bool> DeleteInstancePhysicalAsync(Guid instanceId);

        /// <summary>Ghi instance + task + history trong một transaction Oracle.</summary>
        Task CreateSubmitBatchAsync(WorkflowInstance instance, WorkflowTask task, WorkflowHistory history);

        /// <summary>Cập nhật task/instance + (tuỳ chọn) tạo task mới + history trong một transaction.</summary>
        Task ExecuteMoveBatchAsync(
            WorkflowTask updatedTask,
            WorkflowInstance updatedInstance,
            WorkflowTask? newPendingTask,
            WorkflowHistory history);
        
        /// <summary>Lấy action mới nhất của instance (Submit/Approve/Reject).</summary>
        Task<string?> GetLastHistoryActionAsync(Guid instanceId);

        /// <summary>Tên bước của task Pending mới nhất (dùng hiển thị).</summary>
        Task<string?> GetPendingStepNameAsync(Guid instanceId);

        /// <summary>Assignee gần nhất của bước (Completed/Returned), chỉ dùng khi trả lại (reject).</summary>
        Task<string?> GetPriorStepAssigneeAsync(Guid instanceId, Guid stepId, string stepName);
        // Tasks
        Task<WorkflowTask?> GetTaskByIdAsync(Guid id);
        Task<IEnumerable<WorkflowTask>> GetPendingTasksByRolesAsync(List<string> roles, bool isAdmin, string userId, Guid? workflowInstanceId = null);
        Task<bool> CreateTaskAsync(WorkflowTask task);
        Task<bool> UpdateTaskAsync(WorkflowTask task);
        
        // History
        Task<IEnumerable<WorkflowHistory>> GetHistoryByInstanceIdAsync(Guid instanceId);
        Task<bool> AddHistoryAsync(WorkflowHistory history);
        
        // Save changes helper
        Task<bool> SaveChangesAsync();
    }
}
