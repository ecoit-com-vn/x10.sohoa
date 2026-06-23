using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IWorkflowIntegrationHandler
    {
        string EntityType { get; }
        Task OnWorkflowStartedAsync(string entityId, Guid instanceId);
        Task OnWorkflowCompletedAsync(string entityId, Guid instanceId);
        Task OnWorkflowRejectedAsync(string entityId, Guid instanceId);
        Task OnWorkflowStateChangedAsync(string entityId, Guid instanceId, string statusName);
        Task<string> GetEntityDetailsAsync(string entityId);
        Task<IReadOnlyDictionary<string, string>> GetEntityDetailsBatchAsync(IReadOnlyCollection<string> entityIds);
    }
}
