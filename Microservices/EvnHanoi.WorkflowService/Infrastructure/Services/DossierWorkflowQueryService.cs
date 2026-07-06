using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Infrastructure.Services;

public class DossierWorkflowQueryService : IDossierWorkflowQueryService
{
    private readonly IWorkflowEngineService _workflowEngine;

    public DossierWorkflowQueryService(IWorkflowEngineService workflowEngine)
    {
        _workflowEngine = workflowEngine;
    }

    public async Task<object?> TryGetWorkflowByEntityAsync(string entityId, int workflowTypeId)
    {
        try
        {
            return await _workflowEngine.GetInstanceStatusByEntityAsync(entityId, workflowTypeId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public Task<IEnumerable<WorkflowHistory>> GetWorkflowHistoryAsync(string entityId, int workflowTypeId) =>
        _workflowEngine.GetHistoryByEntityAsync(entityId, workflowTypeId);
}
