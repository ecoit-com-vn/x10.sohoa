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
        Task<IEnumerable<WorkflowDefinition>> GetDefinitionsByNameAsync(string name);
        Task<(IEnumerable<WorkflowDefinition> Items, int TotalCount)> GetPagedDefinitionsAsync(int page, int pageSize, string? keyword = null, bool? isActive = null);
        Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid id);

        /// <summary>Lấy definition đang active cho một loại entity (dùng bởi Token Relay submit).</summary>
        Task<WorkflowDefinition?> GetActiveDefinitionByEntityTypeAsync(string entityType);
        Task<WorkflowStep?> GetStepByIdAsync(Guid id);
        Task<bool> CreateDefinitionAsync(WorkflowDefinition definition);
        Task<bool> UpdateDefinitionAsync(Guid id, WorkflowDefinition definition);
        Task<bool> DeleteDefinitionAsync(Guid id);
        Task<bool?> ToggleDefinitionStatusAsync(Guid id);
        
        // Instances
        Task<WorkflowInstance?> GetInstanceByEntityAsync(string entityId, string entityType);
        Task<WorkflowInstance?> GetInstanceByIdAsync(Guid instanceId);
        Task<bool> CreateInstanceAsync(WorkflowInstance instance);
        Task<bool> UpdateInstanceAsync(WorkflowInstance instance);
        
        // Tasks
        Task<WorkflowTask?> GetTaskByIdAsync(Guid id);
        Task<IEnumerable<WorkflowTask>> GetPendingTasksByRolesAsync(List<string> roles, bool isAdmin, string userId);
        Task<bool> CreateTaskAsync(WorkflowTask task);
        Task<bool> UpdateTaskAsync(WorkflowTask task);
        
        // History
        Task<IEnumerable<WorkflowHistory>> GetHistoryByInstanceIdAsync(Guid instanceId);
        Task<bool> AddHistoryAsync(WorkflowHistory history);
        
        // Save changes helper
        Task<bool> SaveChangesAsync();
    }
}
