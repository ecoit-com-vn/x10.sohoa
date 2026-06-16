using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/borrow-records")]
    public class BorrowRecordController : ControllerBase
    {
        private readonly IBorrowRecordService _borrowService;

        public BorrowRecordController(IBorrowRecordService borrowService)
        {
            _borrowService = borrowService ?? throw new ArgumentNullException(nameof(borrowService));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] BorrowState? state = null)
        {
            var (items, totalCount) = await _borrowService.GetPagedAsync(page, pageSize, keyword, state);
            return Ok(new { items, totalCount, page, pageSize });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<BorrowRecord>> GetById(Guid id)
        {
            var record = await _borrowService.GetByIdAsync(id);
            if (record == null) return NotFound();
            return Ok(record);
        }

        [HttpPost]
        public async Task<ActionResult<BorrowRecord>> Create([FromBody] BorrowRecord request)
        {
            if (request == null) return BadRequest("Dữ liệu không hợp lệ.");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            try
            {
                var record = await _borrowService.CreateAsync(request, userId);
                return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:guid}/state")]
        public async Task<IActionResult> UpdateState(Guid id, [FromBody] BorrowState newState)
        {
            try
            {
                var success = await _borrowService.UpdateStateAsync(id, newState);
                if (!success) return NotFound();
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("move")]
        public async Task<IActionResult> MoveWorkflow([FromBody] MoveWorkflowRequest request)
        {
            if (request == null) return BadRequest("Dữ liệu không hợp lệ.");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            try
            {
                var instance = await _borrowService.MoveWorkflowAsync(
                    request.DossierId,
                    request.NextNodeId,
                    userId,
                    request.ActionLabel,
                    request.Comment,
                    request.NextAssigneeUserId);

                return Ok(new
                {
                    Success = true,
                    Message = "Chuyển bước quy trình mượn trả thành công.",
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

        [HttpPost("{id:guid}/move")]
        public async Task<IActionResult> MoveWorkflowWithValidation(Guid id, [FromBody] MoveWorkflowRequest request)
        {
            if (request == null) return BadRequest("Dữ liệu không hợp lệ.");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";
            var userRoles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            var isAdmin = User.IsInRole("ADMIN") || userRoles.Contains("ADMIN");

            try
            {
                var instance = await _borrowService.MoveWorkflowWithValidationAsync(
                    id,
                    request.DossierId,
                    request.NextNodeId,
                    userId,
                    userRoles,
                    isAdmin,
                    request.ActionLabel,
                    request.Comment,
                    request.NextAssigneeUserId);

                return Ok(new
                {
                    Success = true,
                    Message = "Chuyển bước quy trình mượn trả thành công.",
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

        [HttpGet("get-my-tasks")]
        public async Task<ActionResult<IEnumerable<object>>> GetMyTasks()
        {
            var userRoles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";
            var isAdmin = User.IsInRole("ADMIN") || userRoles.Contains("ADMIN");
            var tasks = await _borrowService.GetMyTasksAsync(userRoles, isAdmin, userId);
            return Ok(tasks);
        }

        [HttpGet("get-workflow-history/{borrowRecordId:guid}")]
        public async Task<ActionResult<IEnumerable<WorkflowHistory>>> GetWorkflowHistory(Guid borrowRecordId)
        {
            var history = await _borrowService.GetWorkflowHistoryAsync(borrowRecordId);
            return Ok(history);
        }

        [HttpGet("get-workflow-by-entity/{entityId}")]
        public async Task<ActionResult> GetWorkflowByEntity(string entityId, [FromQuery] string entityType = "BorrowRecord")
        {
            try
            {
                var status = await _borrowService.GetWorkflowStatusByEntityAsync(entityId, entityType);
                return Ok(status);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("get-workflow-definition/{definitionId:guid}")]
        public async Task<ActionResult<WorkflowDefinition>> GetWorkflowDefinition(Guid definitionId)
        {
            var def = await _borrowService.GetWorkflowDefinitionAsync(definitionId);
            if (def == null)
            {
                return NotFound(new { Message = $"Không tìm thấy cấu hình quy trình với ID = {definitionId}" });
            }
            return Ok(def);
        }
    }
}
