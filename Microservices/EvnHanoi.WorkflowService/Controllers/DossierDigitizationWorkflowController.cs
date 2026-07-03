using EvnHanoi.Infrastructure.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.WorkflowService.Controllers;

/// <summary>Workflow hồ sơ số hóa — WORKFLOW_TYPE_ID = DossierDigitization (3). Tự động duyệt nếu chưa có definition active.</summary>
[Authorize]
[ApiController]
[Route("api/v1/dossier-digitization-workflow")]
public class DossierDigitizationWorkflowController : DossierWorkflowControllerBase
{
    protected override int WorkflowTypeId => EntityType.DossierDigitization.Id;
    protected override bool AutoApproveWhenNoDefinition => true;

    public DossierDigitizationWorkflowController(
        IWorkflowEngineService workflowEngine,
        IWorkflowDefinitionService workflowDefinitionService,
        IHttpClientFactory httpClientFactory,
        IWorkflowRepository workflowRepository,
        IConfiguration configuration)
        : base(workflowEngine, workflowDefinitionService, httpClientFactory, workflowRepository, configuration)
    {
    }
}
