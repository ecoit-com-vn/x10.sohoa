using EvnHanoi.Infrastructure.Enums;
using EvnHanoi.WorkflowService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.WorkflowService.Infrastructure.Services;

/// <summary>
/// Tích hợp quy trình cho hồ sơ số hóa (WORKFLOW_TYPE_ID = DossierDigitization).
/// Logic sync status giống DossierWorkflowHandler.
/// </summary>
public class DossierDigitizationWorkflowHandler : DossierWorkflowHandler
{
    public override int WorkflowTypeId => EntityType.DossierDigitization.Id;

    public DossierDigitizationWorkflowHandler(
        IWorkflowRepository workflowRepository,
        IHttpClientFactory httpClientFactory,
        IMessageProducer messageProducer,
        ILogger<DossierWorkflowHandler> logger)
        : base(workflowRepository, httpClientFactory, messageProducer, logger)
    {
    }
}
