using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EvnHanoi.WorkflowService.Data;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Controllers
{
    [ApiController]
    [Route("api/v1/workflows")]
    public class WorkflowDefinitionsController : ControllerBase
    {
        private readonly WorkflowDbContext _context;
        private readonly ILogger<WorkflowDefinitionsController> _logger;

        public WorkflowDefinitionsController(WorkflowDbContext context, ILogger<WorkflowDefinitionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/workflows
        // Lấy danh sách tất cả quy trình (có thể lọc theo tên, trạng thái)
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkflowDefinition>>> GetAll(
            [FromQuery] string? keyword = null,
            [FromQuery] bool? isActive = null)
        {
            var query = _context.WorkflowDefinitions
                .Include(w => w.Steps)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(w => w.Name.Contains(keyword) || w.Description.Contains(keyword));

            if (isActive.HasValue)
                query = query.Where(w => w.IsActive == isActive.Value);

            var result = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/workflows/{id}
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorkflowDefinition>> GetById(Guid id)
        {
            var def = await _context.WorkflowDefinitions
                .Include(w => w.Steps.OrderBy(s => s.Order))
                .FirstOrDefaultAsync(w => w.Id == id);

            if (def == null)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            return Ok(def);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/v1/workflows
        // Tạo mới quy trình
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<ActionResult<WorkflowDefinition>> Create([FromBody] WorkflowDefinition dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { Message = "Loại quy trình không được để trống." });

            // Ép buộc kích hoạt: vô hiệu hóa các quy trình cùng tên đang active
            if (dto.ForceActivate)
            {
                var sameName = await _context.WorkflowDefinitions
                    .Where(w => w.Name == dto.Name && w.IsActive)
                    .ToListAsync();
                sameName.ForEach(w => w.IsActive = false);
            }

            dto.Id = Guid.NewGuid();
            dto.CreatedAt = DateTime.UtcNow;
            dto.UpdatedAt = DateTime.UtcNow;

            if (dto.Steps != null)
            {
                foreach (var step in dto.Steps)
                {
                    step.Id = Guid.NewGuid();
                    step.WorkflowDefinitionId = dto.Id;
                }
            }

            _context.WorkflowDefinitions.Add(dto);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Quy trình mới được tạo: {Name} v{Version}", dto.Name, dto.Version);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUT /api/v1/workflows/{id}
        // Cập nhật quy trình
        // ─────────────────────────────────────────────────────────────────────
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] WorkflowDefinition dto)
        {
            var existing = await _context.WorkflowDefinitions
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (existing == null)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            // Ép buộc kích hoạt khi update
            if (dto.ForceActivate && dto.IsActive)
            {
                var sameName = await _context.WorkflowDefinitions
                    .Where(w => w.Name == dto.Name && w.IsActive && w.Id != id)
                    .ToListAsync();
                sameName.ForEach(w => w.IsActive = false);
            }

            existing.Name        = dto.Name;
            existing.Description = dto.Description;
            existing.Version     = dto.Version;
            existing.ForceActivate = dto.ForceActivate;
            existing.IsActive    = dto.IsActive;
            existing.UpdatedAt   = DateTime.UtcNow;

            // Cập nhật steps: xóa cũ, thêm mới
            _context.WorkflowSteps.RemoveRange(existing.Steps);
            existing.Steps.Clear();

            if (dto.Steps != null)
            {
                foreach (var step in dto.Steps)
                {
                    step.Id = Guid.NewGuid();
                    step.WorkflowDefinitionId = id;
                    existing.Steps.Add(step);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Quy trình cập nhật: {Name} v{Version}", existing.Name, existing.Version);
            return Ok(existing);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE /api/v1/workflows/{id}
        // ─────────────────────────────────────────────────────────────────────
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var def = await _context.WorkflowDefinitions.FindAsync(id);
            if (def == null)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            _context.WorkflowDefinitions.Remove(def);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Quy trình đã xóa: {Name}", def.Name);
            return Ok(new { Message = $"Đã xóa quy trình: {def.Name}" });
        }

        // ─────────────────────────────────────────────────────────────────────
        // PATCH /api/v1/workflows/{id}/toggle-status
        // Bật/tắt trạng thái hoạt động nhanh
        // ─────────────────────────────────────────────────────────────────────
        [HttpPatch("{id:guid}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var def = await _context.WorkflowDefinitions.FindAsync(id);
            if (def == null)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            def.IsActive  = !def.IsActive;
            def.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Id = id, IsActive = def.IsActive });
        }
    }
}
