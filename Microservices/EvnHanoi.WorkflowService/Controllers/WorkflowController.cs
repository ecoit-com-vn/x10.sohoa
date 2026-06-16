using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace EvnHanoi.WorkflowService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/workflows")]
    public class WorkflowController : ControllerBase
    {
        private readonly IWorkflowEngineService _workflowEngine;

        public WorkflowController(IWorkflowEngineService workflowEngine)
        {
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitToWorkflow([FromQuery] Guid definitionId, [FromQuery] string dossierId, [FromQuery] string entityType = "BorrowRecord")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            try
            {
                var instance = await _workflowEngine.SubmitAsync(definitionId, dossierId, entityType, userId);
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


        [HttpPost("move")]
        public async Task<IActionResult> MoveWorkflow([FromBody] MoveWorkflowRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            try
            {
                var instance = await _workflowEngine.MoveAsync(request.DossierId, request.NextNodeId, userId, request.ActionLabel, request.Comment, request.NextAssigneeUserId);
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
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
    }
}
