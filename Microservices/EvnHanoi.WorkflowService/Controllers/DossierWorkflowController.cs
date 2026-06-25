using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Controllers;

/// <summary>
/// Cửa ngõ API quy trình của hồ sơ — đã tách từ EquipmentService sang WorkflowService.
/// KHÔNG gắn [WorkflowEngineApi] để DynamicPermissionFilter ánh xạ về quyền DOSSIER_*
/// (DossierWorkflow => DOSSIER): Submit=>DOSSIER_CREATE, Move=>DOSSIER_MANAGE, Get*=>DOSSIER_VIEW.
/// Gọi IWorkflowEngineService in-process (không gọi HTTP api/v1/workflows/* của chính mình).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/dossiers-workflow")]
public class DossierWorkflowController : ControllerBase
{
    private const string DossierEntityType = "Dossier";

    private readonly IWorkflowEngineService _workflowEngine;
    private readonly IWorkflowDefinitionService _workflowDefinitionService;

    public DossierWorkflowController(
        IWorkflowEngineService workflowEngine,
        IWorkflowDefinitionService workflowDefinitionService)
    {
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _workflowDefinitionService = workflowDefinitionService ?? throw new ArgumentNullException(nameof(workflowDefinitionService));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "system";

    private List<string> UserRoles => User.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList();

    private bool IsAdmin => User.IsInRole("ADMIN") || UserRoles.Contains("ADMIN");

    // ===== GỬI DUYỆT (POST submit => DOSSIER_CREATE) =====

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        try
        {
            var instance = await _workflowEngine.SubmitByEntityTypeAsync(id.ToString(), DossierEntityType, UserId);
            return Ok(new
            {
                success = true,
                message = "Gửi duyệt hồ sơ thành công.",
                data = new
                {
                    instanceId = instance.Id,
                    dossierStatus = "PendingApproval",
                    workflowStepName = instance.CurrentNodeName
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ===== CHUYỂN BƯỚC (POST move => DOSSIER_MANAGE) =====

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MoveWorkflow(Guid id, [FromBody] MoveDossierWorkflowRequest request)
    {
        if (request == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var instance = await _workflowEngine.MoveWithValidationAsync(
                id.ToString(),
                request.NextNodeId,
                UserId,
                UserRoles,
                IsAdmin,
                request.ActionLabel,
                request.Comment,
                request.NextAssigneeUserId,
                DossierEntityType);

            object? workflow = null;
            try
            {
                workflow = await _workflowEngine.GetInstanceStatusByEntityAsync(id.ToString(), DossierEntityType);
            }
            catch (KeyNotFoundException)
            {
                // Instance có thể đã kết thúc — bỏ qua snapshot workflow.
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    status = instance.Status,
                    currentNodeName = instance.CurrentNodeName,
                    workflow
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ===== TRẠNG THÁI / LỊCH SỬ / TASK (GET => DOSSIER_VIEW) =====

    [HttpGet("{id:guid}/get-workflow-by-entity")]
    public async Task<IActionResult> GetWorkflowByEntity(Guid id)
    {
        try
        {
            var status = await _workflowEngine.GetInstanceStatusByEntityAsync(id.ToString(), DossierEntityType);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            // Hồ sơ chưa vào quy trình — tương thích endpoint cũ ở EquipmentService (trả 200 + null, không 404).
            return Ok(null);
        }
    }

    [HttpGet("{id:guid}/get-workflow-history")]
    public async Task<IActionResult> GetWorkflowHistory(Guid id)
    {
        try
        {
            var history = await _workflowEngine.GetHistoryByEntityAsync(id.ToString(), DossierEntityType);
            return Ok(history);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("get-workflow-definition/{definitionId:guid}")]
    public async Task<IActionResult> GetWorkflowDefinition(Guid definitionId)
    {
        var def = await _workflowDefinitionService.GetDefinitionByIdAsync(definitionId);
        if (def == null) return NotFound(new { message = $"Không tìm thấy định nghĩa quy trình với ID = {definitionId}" });
        return Ok(def);
    }

    [HttpGet("get-my-tasks")]
    public async Task<IActionResult> GetMyTasks([FromQuery] Guid? instanceId = null)
    {
        var tasks = await _workflowEngine.GetMyTasksAsync(UserRoles, IsAdmin, UserId, instanceId);
        return Ok(tasks);
    }
}

/// <summary>Request chuyển bước workflow cho hồ sơ.</summary>
public class MoveDossierWorkflowRequest
{
    public string NextNodeId { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? NextAssigneeUserId { get; set; }
}
