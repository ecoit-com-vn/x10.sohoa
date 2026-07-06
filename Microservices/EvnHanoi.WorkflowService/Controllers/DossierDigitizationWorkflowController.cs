using EvnHanoi.Infrastructure.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.WorkflowService.Controllers;

/// <summary>Workflow hồ sơ số hóa — WORKFLOW_TYPE_ID = DossierDigitization (3). Tự động duyệt nếu chưa có definition active.</summary>
/// <remarks>
/// GET /api/v1/dossier-digitization-workflow/{id}/get-workflow-by-entity — đọc instance WF (WorkflowTypeId=3).
/// GET /api/v1/dossier-digitization-workflow/{id}/get-workflow-history — lịch sử WF.
/// </remarks>
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
        IDossierWorkflowQueryService dossierWorkflowQuery,
        IHttpClientFactory httpClientFactory,
        IWorkflowRepository workflowRepository,
        IConfiguration configuration)
        : base(workflowEngine, workflowDefinitionService, dossierWorkflowQuery, httpClientFactory, workflowRepository, configuration)
    {
    }
}
