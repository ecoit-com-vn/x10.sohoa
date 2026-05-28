using System;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.DigitizationService.Controllers
{
    [ApiController]
    [Route("api/v1/digitization-task")]
    public class DigitizationTaskController : ControllerBase
    {
        private readonly IDigitizationTaskRepository _taskRepository;
        private readonly ILogger<DigitizationTaskController> _logger;

        public DigitizationTaskController(
            IDigitizationTaskRepository taskRepository,
            ILogger<DigitizationTaskController> logger)
        {
            _taskRepository = taskRepository;
            _logger = logger;
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignTask([FromBody] AssignTaskRequest request)
        {
            if (string.IsNullOrEmpty(request.DossierId) || string.IsNullOrEmpty(request.AssignedToUserId))
            {
                return BadRequest("DossierId and AssignedToUserId are required.");
            }

            try
            {
                var task = new DigitizationTask
                {
                    Id = Guid.NewGuid(),
                    DossierId = request.DossierId,
                    WorkflowStepId = request.WorkflowStepId,
                    AssignedToUserId = request.AssignedToUserId,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    Notes = request.Notes ?? string.Empty
                };

                var taskId = await _taskRepository.CreateAsync(task);
                
                return Ok(new { TaskId = taskId, Message = "Task assigned successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while assigning task");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTask(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null)
            {
                return NotFound();
            }
            return Ok(task);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetTasksByUser(string userId)
        {
            var tasks = await _taskRepository.GetByUserIdAsync(userId);
            return Ok(tasks);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateTaskStatus(Guid id, [FromBody] UpdateTaskStatusRequest request)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            task.Status = request.Status;
            task.Notes = request.Notes ?? task.Notes;
            if (request.Status == "Completed" || request.Status == "Failed")
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _taskRepository.UpdateAsync(task);

            return NoContent();
        }
    }

    public class AssignTaskRequest
    {
        public string DossierId { get; set; } = string.Empty;
        public Guid WorkflowStepId { get; set; }
        public string AssignedToUserId { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class UpdateTaskStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
