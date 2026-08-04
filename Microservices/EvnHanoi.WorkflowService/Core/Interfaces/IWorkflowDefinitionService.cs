using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IWorkflowDefinitionService
    {
        Task<(IEnumerable<WorkflowDefinition> Items, int TotalCount)> GetPagedDefinitionsAsync(int page, int pageSize, string? keyword = null, bool? isActive = null);
        Task<WorkflowDefinition?> UpdateDefinitionWithVersioningAsync(Guid id, WorkflowDefinition dto, string userId);
        Task<WorkflowDefinition?> GetLatestActiveDefinitionByNameAsync(string name);
        Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid id);
        Task<bool> ReactivateDefinitionAsync(Guid id, int workflowTypeId, string name);
    }
}
