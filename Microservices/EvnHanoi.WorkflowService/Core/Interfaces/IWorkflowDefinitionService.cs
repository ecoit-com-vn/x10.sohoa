using EvnHanoi.WorkflowService.Models;
using System;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IWorkflowDefinitionService
    {
        Task<WorkflowDefinition?> UpdateDefinitionWithVersioningAsync(Guid id, WorkflowDefinition dto, string userId);
        Task<WorkflowDefinition?> GetLatestActiveDefinitionByNameAsync(string name);
        Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid id);
        Task<bool> ReactivateDefinitionAsync(Guid id, int workflowTypeId, string name);
    }
}
