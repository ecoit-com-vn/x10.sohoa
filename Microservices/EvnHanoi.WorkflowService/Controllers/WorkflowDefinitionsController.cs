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
            var (items, totalCount) = await _workflowRepository.GetPagedDefinitionsAsync(page, pageSize, keyword, isActive);
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
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { Message = "Loại quy trình không được để trống." });

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
                NormalizeEntityType(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
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

            NormalizeEntityType(dto);
            
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

            var success = await _workflowRepository.DeleteDefinitionAsync(id);
            if (!success)
            {
                return BadRequest(new { Message = "Không thể xóa quy trình." });
            }

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
            var newStatus = await _workflowRepository.ToggleDefinitionStatusAsync(id);
            if (!newStatus.HasValue)
                return NotFound(new { Message = $"Không tìm thấy quy trình với ID = {id}" });

            var def = await _workflowRepository.GetDefinitionByIdAsync(id, includeBpmnXml: false);
            if (def != null && !string.IsNullOrWhiteSpace(def.EntityType))
                _definitionCache.InvalidateActiveDefinition(def.EntityType);

            return Ok(new { Id = id, IsActive = newStatus.Value });
        }

        private static void NormalizeEntityType(WorkflowDefinition dto)
        {
            if (string.IsNullOrWhiteSpace(dto.EntityType))
                throw new InvalidOperationException("EntityType không được để trống.");

            var item = EntityType.RequireByCode(dto.EntityType);
            dto.EntityType = item.Code;
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

        [HttpGet("versions/{name}")]
        [BypassDynamicPermission]
        public async Task<IActionResult> GetVersions(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { Message = "Tên quy trình không được để trống." });

            var versions = await _workflowRepository.GetDefinitionsByNameAsync(name);
            return Ok(versions);
        }
    }
}
