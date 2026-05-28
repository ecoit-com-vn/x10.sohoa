using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EvnHanoi.WorkflowService.Data;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowDefinitionsController : ControllerBase
    {
        private readonly WorkflowDbContext _context;

        public WorkflowDefinitionsController(WorkflowDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkflowDefinition>>> GetWorkflowDefinitions()
        {
            return await _context.WorkflowDefinitions.Include(w => w.Steps).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WorkflowDefinition>> GetWorkflowDefinition(Guid id)
        {
            var workflowDefinition = await _context.WorkflowDefinitions
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workflowDefinition == null)
            {
                return NotFound();
            }

            return workflowDefinition;
        }

        [HttpPost]
        public async Task<ActionResult<WorkflowDefinition>> CreateWorkflowDefinition(WorkflowDefinition workflowDefinition)
        {
            workflowDefinition.Id = Guid.NewGuid();
            if (workflowDefinition.Steps != null)
            {
                foreach (var step in workflowDefinition.Steps)
                {
                    step.Id = Guid.NewGuid();
                    step.WorkflowDefinitionId = workflowDefinition.Id;
                }
            }

            _context.WorkflowDefinitions.Add(workflowDefinition);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetWorkflowDefinition), new { id = workflowDefinition.Id }, workflowDefinition);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkflowDefinition(Guid id, WorkflowDefinition workflowDefinition)
        {
            if (id != workflowDefinition.Id)
            {
                return BadRequest();
            }

            var existingDef = await _context.WorkflowDefinitions.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Id == id);
            if (existingDef == null)
            {
                return NotFound();
            }

            existingDef.Name = workflowDefinition.Name;
            existingDef.Description = workflowDefinition.Description;
            existingDef.IsActive = workflowDefinition.IsActive;

            // Simple replace steps
            _context.WorkflowSteps.RemoveRange(existingDef.Steps);
            if (workflowDefinition.Steps != null)
            {
                foreach (var step in workflowDefinition.Steps)
                {
                    step.Id = Guid.NewGuid();
                    step.WorkflowDefinitionId = id;
                    existingDef.Steps.Add(step);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!WorkflowDefinitionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkflowDefinition(Guid id)
        {
            var workflowDefinition = await _context.WorkflowDefinitions.FindAsync(id);
            if (workflowDefinition == null)
            {
                return NotFound();
            }

            _context.WorkflowDefinitions.Remove(workflowDefinition);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool WorkflowDefinitionExists(Guid id)
        {
            return _context.WorkflowDefinitions.Any(e => e.Id == id);
        }
    }
}
