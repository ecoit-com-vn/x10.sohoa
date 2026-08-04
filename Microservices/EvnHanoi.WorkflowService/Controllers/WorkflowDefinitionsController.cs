using EvnHanoi.Infrastructure.Enums;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using EvnHanoi.WorkflowService.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using EvnHanoi.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/WorkflowDefinitions")]
    public class WorkflowDefinitionsController : ControllerBase
    {
        private readonly IWorkflowRepository _workflowRepository;
        private readonly IBpmnValidatorService _bpmnValidatorService;
        private readonly IWorkflowDefinitionService _workflowDefinitionService;
        private readonly ILogger<WorkflowDefinitionsController> _logger;
        private readonly WorkflowDefinitionCacheService _definitionCache;

        public WorkflowDefinitionsController(
            IWorkflowRepository workflowRepository, 
            IBpmnValidatorService bpmnValidatorService,
            IWorkflowDefinitionService workflowDefinitionService,
            ILogger<WorkflowDefinitionsController> logger,
            WorkflowDefinitionCacheService definitionCache)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _bpmnValidatorService = bpmnValidatorService ?? throw new ArgumentNullException(nameof(bpmnValidatorService));
            _workflowDefinitionService = workflowDefinitionService ?? throw new ArgumentNullException(nameof(workflowDefinitionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _definitionCache = definitionCache ?? throw new ArgumentNullException(nameof(definitionCache));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/workflows (routed via ApiGateway, path in controller is api/WorkflowDefinitions)
        // Lấy danh sách tất cả quy trình (có thể lọc theo tên, trạng thái)
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? keyword = null,
            [FromQuery] bool? isActive = null)
        {
            var (items, totalCount) = await _workflowDefinitionService.GetPagedDefinitionsAsync(page, pageSize, keyword, isActive);
            return Ok(new { items, totalCount, page, pageSize });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/workflows/{id}
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorkflowDefinition>> GetById(Guid id)
        {
            var def = await _workflowRepository.GetDefinitionByIdAsync(id);

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
            if (dto.WorkflowTypeId <= 0)
                return BadRequest(new { Message = "WorkflowTypeId không hợp lệ." });

            var xmlErrors = _bpmnValidatorService.Validate(dto.BpmnXml);
            if (xmlErrors.Any())
            {
                return BadRequest(new { Message = "Sơ đồ quy trình không hợp lệ: " + string.Join("; ", xmlErrors), Errors = xmlErrors });
            }

            dto.Id = Guid.CreateVersion7();
            dto.CreatedAt = DateTime.UtcNow;
            dto.UpdatedAt = DateTime.UtcNow;

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";
            dto.CreatedBy = userId;
            dto.UpdatedBy = userId;

            try
            {
                NormalizeWorkflowType(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }

            if (await _workflowRepository.ExistsDefinitionByWorkflowTypeIdAsync(dto.WorkflowTypeId))
            {
                var typeItem = EntityType.RequireById(dto.WorkflowTypeId);
                return BadRequest(new { Message = $"Quy trình cho loại '{typeItem.Name}' đã tồn tại. Bạn không thể tạo quy trình mới cùng loại quy trình này, chỉ được phép sửa quy trình hiện có." });
            }

            if (dto.Steps != null)
            {
                foreach (var step in dto.Steps)
                {
                    step.Id = Guid.CreateVersion7();
                    step.WorkflowDefinitionId = dto.Id;
                }
            }

            var success = await _workflowRepository.CreateDefinitionAsync(dto);
            if (!success)
            {
                return BadRequest(new { Message = "Không thể tạo quy trình." });
            }

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
            var xmlErrors = _bpmnValidatorService.Validate(dto.BpmnXml);
            if (xmlErrors.Any())
            {
                return BadRequest(new { Message = "Sơ đồ quy trình không hợp lệ: " + string.Join("; ", xmlErrors), Errors = xmlErrors });
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "admin";

            NormalizeWorkflowType(dto);
            
            try
            {
                var updated = await _workflowDefinitionService.UpdateDefinitionWithVersioningAsync(id, dto, userId);
                if (updated == null)
                {
                    return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });
                }
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE /api/v1/workflows/{id}
        // ─────────────────────────────────────────────────────────────────────
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var def = await _workflowRepository.GetDefinitionByIdAsync(id);
            if (def == null)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            if (def.IsActive)
            {
                return BadRequest(new { Message = "Không thể xóa quy trình đang hoạt động. Vui lòng tắt trạng thái hoạt động trước khi xóa." });
            }

            var success = await _workflowRepository.DeleteDefinitionAsync(id);
            if (!success)
            {
                return BadRequest(new { Message = "Không thể xóa quy trình." });
            }

            _logger.LogInformation("Quy trình đã xóa mềm: {Name}", def.Name);
            return Ok(new { Message = $"Đã xóa quy trình: {def.Name}" });
        }

        // ─────────────────────────────────────────────────────────────────────
        // PATCH /api/v1/workflows/{id}/toggle-status
        // Bật/tắt trạng thái hoạt động nhanh
        // ─────────────────────────────────────────────────────────────────────
        [HttpPatch("{id:guid}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var newStatus = await _workflowRepository.ToggleDefinitionStatusAsync(id);
            if (!newStatus.HasValue)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            var def = await _workflowRepository.GetDefinitionByIdAsync(id, includeBpmnXml: false);
            if (def != null && def.WorkflowTypeId > 0)
                _definitionCache.InvalidateActiveDefinition(def.WorkflowTypeId);

            return Ok(new { Id = id, IsActive = newStatus.Value });
        }

        [HttpPost("{id:guid}/reactivate")]
        public async Task<IActionResult> Reactivate(Guid id)
        {
            var def = await _workflowRepository.GetDefinitionByIdAsync(id);
            if (def == null)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            var success = await _workflowDefinitionService.ReactivateDefinitionAsync(id, def.WorkflowTypeId, def.Name);
            if (!success)
                return BadRequest(new { Message = "Không thể tái kích hoạt quy trình." });

            if (def.WorkflowTypeId > 0)
                _definitionCache.InvalidateActiveDefinition(def.WorkflowTypeId);

            _logger.LogInformation("Quy trình đã tái kích hoạt: {Name} v{Version}", def.Name, def.Version);
            return Ok(new { Message = $"Đã tái kích hoạt quy trình: {def.Name} v{def.Version}", Id = id });
        }

        private static void NormalizeWorkflowType(WorkflowDefinition dto)
        {
            if (dto.WorkflowTypeId <= 0)
                throw new InvalidOperationException("WorkflowTypeId không hợp lệ.");

            var item = EntityType.RequireById(dto.WorkflowTypeId);
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                dto.Name = item.Name;
            }
        }

        [HttpGet("get-workflow-type")]
        [BypassDynamicPermission]
        public ActionResult<IEnumerable<object>> GetWorkflowTypes()
        {
            var items = EntityType.GetAll()
                .Select(x => new { x.Id, x.Code, x.Name })
                .ToList();
            return Ok(items);
        }

        [HttpGet("versions/{workflowTypeId:int}")]
        [BypassDynamicPermission]
        public async Task<IActionResult> GetVersions(int workflowTypeId)
        {
            if (EntityType.TryGetById(workflowTypeId) == null)
                return BadRequest(new { Message = $"WorkflowTypeId không hợp lệ: '{workflowTypeId}'." });

            var versions = await _workflowRepository.GetDefinitionsByWorkflowTypeIdAsync(workflowTypeId);
            return Ok(versions);
        }
    }
}
