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
    }
}
