using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Core.Interfaces;

/// <summary>Đọc workflow theo entity — dùng chung cho DossierWorkflow và DossierDigitizationWorkflow.</summary>
public interface IDossierWorkflowQueryService
{
    Task<object?> TryGetWorkflowByEntityAsync(string entityId, int workflowTypeId);

    Task<IEnumerable<WorkflowHistory>> GetWorkflowHistoryAsync(string entityId, int workflowTypeId);
}
