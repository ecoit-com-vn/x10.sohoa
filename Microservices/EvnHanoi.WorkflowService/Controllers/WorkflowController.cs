using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace EvnHanoi.WorkflowService.Controllers
{
    /// <summary>
    /// API engine quy trình — không có menu/ma trận quyền động riêng.
    /// Phân quyền: JWT bắt buộc + kiểm tra vai trò/task trong WorkflowEngineService.
    /// FE/domain service (Dossier, BorrowRecord) kiểm tra quyền nghiệp vụ trước khi gọi relay.
    /// </summary>
    [Authorize]
    [WorkflowEngineApi]
    [ApiController]
    [Route("api/v1/workflows")]
    public class WorkflowController : ControllerBase
    {
        private readonly IWorkflowEngineService _workflowEngine;
        private readonly IWorkflowDefinitionService _workflowDefinitionService;

        public WorkflowController(
            IWorkflowEngineService workflowEngine,
            IWorkflowDefinitionService workflowDefinitionService)
        {
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
            _workflowDefinitionService = workflowDefinitionService ?? throw new ArgumentNullException(nameof(workflowDefinitionService));
        }

        [HttpGet("get-workflow-by-entity/{entityId}")]
        public async Task<ActionResult> GetWorkflowByEntity(string entityId, [FromQuery] int? workflowTypeId)
        {
            try
            {
                var typeId = workflowTypeId ?? EntityType.BorrowRecord.Id;
                var status = await _workflowEngine.GetInstanceStatusByEntityAsync(entityId, typeId);
                return Ok(status);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("get-workflow-definition/{definitionId:guid}")]
        public async Task<IActionResult> GetWorkflowDefinition(Guid definitionId)
        {
            var definition = await _workflowDefinitionService.GetDefinitionByIdAsync(definitionId);
            if (definition == null)
                return NotFound(new { message = $"Không tìm thấy workflow definition với ID = {definitionId}." });

            return Ok(definition);
        }

        /// <summary>
        /// Khởi chạy quy trình — thường được gọi qua Token Relay từ domain service
        /// sau khi domain controller đã kiểm tra quyền (vd: DOSSIER_CREATE).
        /// </summary>
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitToWorkflow([FromBody] SubmitWorkflowRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            try
            {
            if (EntityType.TryGetById(request.WorkflowTypeId) == null)
                return BadRequest(new { message = $"WorkflowTypeId không hợp lệ: '{request.WorkflowTypeId}'." });

                var instance = await _workflowEngine.SubmitByWorkflowTypeIdAsync(
                    request.EntityId,
                    request.WorkflowTypeId,
                    userId);
                return Ok(new
                {
                    Success = true,
                    Message = "Khởi chạy quy trình phê duyệt thành công.",
                    InstanceId = instance.Id,
                    Status = instance.Status
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

        [HttpGet("get-my-tasks")]
        public async Task<IActionResult> GetMyTasks([FromQuery] Guid? instanceId = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";
            var roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            var isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
                                      || r.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            var tasks = await _workflowEngine.GetMyTasksAsync(roles, isAdmin, userId, instanceId);
            return Ok(tasks);
        }

        [HttpGet("get-workflow-history/{entityId}")]
        public async Task<IActionResult> GetWorkflowHistory(string entityId, [FromQuery] int? workflowTypeId)
        {
            try
            {
                var typeId = workflowTypeId ?? EntityType.Dossier.Id;
                var history = await _workflowEngine.GetHistoryByEntityAsync(entityId, typeId);
                return Ok(history);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("tasks/{taskId:guid}/approve")]
        public async Task<IActionResult> ApproveTask(Guid taskId, [FromBody] ApproveTaskRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            try
            {
                var task = await _workflowEngine.ApproveAsync(taskId, userId, request.Comment, request.NextAssigneeUserId);
                return Ok(new
                {
                    Success = true,
                    Message = "Đã phê duyệt nhiệm vụ thành công.",
                    Status = task.WorkflowInstance?.Status ?? "Completed"
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

        [HttpPost("tasks/{taskId:guid}/reject")]
        public async Task<IActionResult> RejectTask(Guid taskId, [FromBody] string? comment = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            try
            {
                var prevTask = await _workflowEngine.RejectAsync(taskId, userId, comment);
                return Ok(new
                {
                    Success = true,
                    Message = prevTask != null 
                        ? $"Nhiệm vụ đã được trả lại bước trước đó: '{prevTask.StepName}'."
                        : "Quy trình phê duyệt đã bị từ chối và chấm dứt.",
                    Status = prevTask != null ? "Running" : "Terminated"
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

        /// <summary>
        /// Chuyển bước — kiểm tra vai trò BPMN và đường đi hợp lệ trước khi thực thi.
        /// </summary>
        [HttpPost("move")]
        public async Task<IActionResult> MoveWorkflow([FromBody] MoveWorkflowRequest request)
        {
            if (request == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";
            var roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            var isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
                                      || r.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            try
            {
                var instance = await _workflowEngine.MoveWithValidationAsync(
                    request.DossierId,
                    request.NextNodeId,
                    userId,
                    roles,
                    isAdmin,
                    request.ActionLabel,
                    request.Comment,
                    request.NextAssigneeUserId,
                    request.WorkflowTypeId ?? EntityType.BorrowRecord.Id);

                return Ok(new
                {
                    Success = true,
                    Message = "Chuyển bước quy trình thành công.",
                    Status = instance.Status
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
    }

    public class SubmitWorkflowRequest
    {
        public string EntityId { get; set; } = string.Empty;

        /// <summary>ID loại quy trình liên kết với WORKFLOW_TYPES</summary>
        public int WorkflowTypeId { get; set; }
    }

    public class ApproveTaskRequest
    {
        public string? Comment { get; set; }
        public string? NextAssigneeUserId { get; set; }
    }

    public class MoveWorkflowRequest
    {
        public string DossierId { get; set; } = string.Empty;
        public string NextNodeId { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public string? NextAssigneeUserId { get; set; }
        /// <summary>WorkflowTypeId của WorkflowInstance — mặc định BorrowRecord.</summary>
        public int? WorkflowTypeId { get; set; }
    }
}
