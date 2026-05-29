using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EvnHanoi.WorkflowService.Data;
using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Controllers
{
    [ApiController]
    [Route("api/v1/workflows")]
    public class WorkflowController : ControllerBase
    {
        private readonly WorkflowDbContext _dbContext;

        public WorkflowController(WorkflowDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BorrowRecord>>> GetAll()
        {
            return await _dbContext.BorrowRecords.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BorrowRecord>> GetById(Guid id)
        {
            var record = await _dbContext.BorrowRecords.FindAsync(id);
            if (record == null) return NotFound();
            return record;
        }

        [HttpPost]
        public async Task<ActionResult<BorrowRecord>> Create(BorrowRecord request)
        {
            request.Id = Guid.NewGuid();
            request.RequestDate = DateTime.UtcNow;
            request.State = BorrowState.Requested;

            _dbContext.BorrowRecords.Add(request);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
        }

        [HttpPut("{id}/state")]
        public async Task<IActionResult> UpdateState(Guid id, [FromBody] BorrowState newState)
        {
            var record = await _dbContext.BorrowRecords.FindAsync(id);
            if (record == null) return NotFound();

            // Simple State Machine validation
            if (record.State == BorrowState.Requested && newState == BorrowState.Approved)
            {
                record.State = BorrowState.Approved;
                record.ApprovedDate = DateTime.UtcNow;
            }
            else if (record.State == BorrowState.Approved && newState == BorrowState.Borrowed)
            {
                record.State = BorrowState.Borrowed;
                record.BorrowedDate = DateTime.UtcNow;
            }
            else if (record.State == BorrowState.Borrowed && newState == BorrowState.Returned)
            {
                record.State = BorrowState.Returned;
                record.ReturnedDate = DateTime.UtcNow;
            }
            else
            {
                return BadRequest("Invalid state transition.");
            }

            await _dbContext.SaveChangesAsync();
            return NoContent();
        }

        // --- BPMN Workflow Engine (Lỗ hổng 5) ---

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitToWorkflow([FromQuery] Guid definitionId, [FromQuery] string dossierId)
        {
            var definition = await _dbContext.WorkflowDefinitions
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.Id == definitionId);
                
            if (definition == null)
            {
                return NotFound(new { message = "Không tìm thấy định nghĩa quy trình BPMN." });
            }

            var steps = definition.Steps;
            if (steps == null || steps.Count == 0)
            {
                return BadRequest(new { message = "Quy trình chưa cấu hình bất kỳ bước duyệt nào." });
            }

            var firstStep = await _dbContext.WorkflowSteps
                .Where(s => s.WorkflowDefinitionId == definitionId)
                .OrderBy(s => s.Order)
                .FirstOrDefaultAsync();

            if (firstStep == null)
            {
                return BadRequest(new { message = "Không tìm thấy bước đầu tiên của quy trình." });
            }

            return Ok(new
            {
                Success = true,
                Message = $"Hồ sơ {dossierId} đã được đưa vào quy trình '{definition.Name}' thành công.",
                CurrentStepId = firstStep.Id,
                CurrentStep = firstStep.StepName,
                AssignedRole = firstStep.RequiredRole,
                ActionRequired = firstStep.ActionType,
                Status = "InProgress"
            });
        }

        [HttpPost("advance")]
        public async Task<IActionResult> AdvanceWorkflow([FromQuery] Guid definitionId, [FromQuery] string dossierId, [FromQuery] Guid currentStepId)
        {
            var currentStep = await _dbContext.WorkflowSteps.FindAsync(currentStepId);
            if (currentStep == null)
            {
                return NotFound(new { message = "Không tìm thấy bước quy trình hiện tại." });
            }

            var nextStep = await _dbContext.WorkflowSteps
                .Where(s => s.WorkflowDefinitionId == definitionId && s.Order > currentStep.Order)
                .OrderBy(s => s.Order)
                .FirstOrDefaultAsync();

            if (nextStep == null)
            {
                return Ok(new
                {
                    Success = true,
                    Message = "Quy trình số hóa hồ sơ đã hoàn thành thành công.",
                    Status = "Completed"
                });
            }

            return Ok(new
            {
                Success = true,
                Message = $"Hồ sơ {dossierId} đã chuyển sang bước tiếp theo thành công.",
                CurrentStepId = nextStep.Id,
                CurrentStep = nextStep.StepName,
                AssignedRole = nextStep.RequiredRole,
                ActionRequired = nextStep.ActionType,
                Status = "InProgress"
            });
        }
    }
}
