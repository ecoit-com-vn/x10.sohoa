using EvnHanoi.Infrastructure.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.WorkflowService.Controllers;

/// <summary>Workflow hồ sơ mới — WORKFLOW_TYPE_ID = Dossier (1).</summary>
[Authorize]
[ApiController]
[Route("api/v1/dossiers-workflow")]
public class DossierWorkflowController : DossierWorkflowControllerBase
{
    protected override int WorkflowTypeId => EntityType.Dossier.Id;
    protected override bool AutoApproveWhenNoDefinition => false;

    public DossierWorkflowController(
        IWorkflowEngineService workflowEngine,
        IWorkflowDefinitionService workflowDefinitionService,
        IHttpClientFactory httpClientFactory,
        IWorkflowRepository workflowRepository,
        IConfiguration configuration)
        : base(workflowEngine, workflowDefinitionService, httpClientFactory, workflowRepository, configuration)
    {
    }
}
