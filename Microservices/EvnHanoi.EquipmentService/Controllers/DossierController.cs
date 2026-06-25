using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/dossiers")]
public partial class DossierController : ControllerBase
{
    private readonly IDossierService _dossierService;
    private readonly IDossierDocumentService _dossierDocumentService;
    private readonly IDocumentDigitizationService _documentDigitizationService;

    public DossierController(
        IDossierService dossierService,
        IDossierDocumentService dossierDocumentService,
        IDocumentDigitizationService documentDigitizationService)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
        _dossierDocumentService = dossierDocumentService ?? throw new ArgumentNullException(nameof(dossierDocumentService));
        _documentDigitizationService = documentDigitizationService ?? throw new ArgumentNullException(nameof(documentDigitizationService));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? User.Identity?.Name ?? "system";

    private string UserName => User.FindFirst("preferred_username")?.Value
                             ?? User.FindFirst(ClaimTypes.Name)?.Value
                             ?? User.Identity?.Name ?? "system";

    private string UserFullName => User.FindFirst("name")?.Value
                                 ?? User.FindFirst(ClaimTypes.GivenName)?.Value
                                 ?? UserName;

    // ===== DANH SÁCH =====

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? gridTypeId,
        [FromQuery] long? unitId,
        [FromQuery] string? status,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var filter = new DossierFilterDto
        {
            Keyword = keyword,
            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = unitId,
            Status = status,
            DossierTypeId = dossierTypeId,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _dossierService.GetPagedAsync(filter);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("grid-types/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetGridTypesLookup()
    {
        var items = await _dossierService.GetGridTypesLookupAsync();
        return Ok(items);
    }

    [HttpGet("infrastructures/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetInfrastructuresLookup()
    {
        var items = await _dossierService.GetInfrastructuresLookupAsync();
        return Ok(items);
    }

    [HttpGet("dossier-type/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDossierTypesLookup()
    {
        var items = await _dossierService.GetDossierTypesLookupAsync();
        return Ok(items);
    }

    // ===== CHI TIẾT =====

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var detail = await _dossierService.GetDetailByIdAsync(id);
        if (detail == null) return NotFound(new { message = $"Không tìm thấy hồ sơ với ID = {id}" });
        return Ok(detail);
    }

    // ===== TẠO MỚI =====

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DossierCreateDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var newId = await _dossierService.CreateAsync(dto, UserId, UserName, UserFullName);
        return CreatedAtAction(nameof(GetDetail), new { id = newId }, new { id = newId });
    }

    // ===== CẬP NHẬT =====

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DossierUpdateDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            await _dossierService.UpdateAsync(id, dto, UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex.Message.Contains("Concurrency"))
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ===== XÓA =====

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _dossierService.DeleteAsync(id, UserId);
            return NoContent();
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

    // ===== GỬI DUYỆT =====

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        try
        {
            var result = await _dossierService.SubmitForApprovalAsync(id, UserId);
            return Ok(new { success = true, message = "Gửi duyệt hồ sơ thành công.", data = result });
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

    // ===== LƯU FORM DATA =====

    [HttpPost("{id:guid}/form-data")]
    public async Task<IActionResult> SaveFormData(Guid id, [FromBody] DossierSaveFormDataDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var result = await _dossierService.SaveFormDataAsync(id, dto, UserId);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex.Message.Contains("Concurrency"))
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ===== PHIÊN BẢN =====

    [HttpGet("{id:guid}/versions")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetVersions(Guid id)
    {
        var versions = await _dossierService.GetVersionsAsync(id);
        return Ok(versions);
    }

    // ===== THIẾT BỊ =====

    [HttpGet("{id:guid}/equipment")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetEquipments(Guid id)
    {
        var equipments = await _dossierService.GetEquipmentsAsync(id);
        return Ok(equipments);
    }

    [HttpPost("{id:guid}/equipment")]
    public async Task<IActionResult> AddEquipment(Guid id, [FromBody] AddDossierEquipmentDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });
        var result = await _dossierService.AddEquipmentAsync(id, dto.EquipmentId);
        return result ? Ok(new { success = true }) : BadRequest(new { message = "Thêm thiết bị thất bại." });
    }

    [HttpDelete("{id:guid}/equipment/{equipmentId:guid}")]
    public async Task<IActionResult> RemoveEquipment(Guid id, Guid equipmentId)
    {
        var result = await _dossierService.RemoveEquipmentAsync(id, equipmentId);
        return result ? NoContent() : NotFound(new { message = "Không tìm thấy thiết bị trong hồ sơ." });
    }

    // ===== WORKFLOW =====

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MoveWorkflow(Guid id, [FromBody] MoveWorkflowDossierRequest request)
    {
        if (request == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var result = await _dossierService.MoveWorkflowAsync(
                id.ToString(),
                request.NextNodeId,
                UserId,
                request.ActionLabel,
                request.Comment,
                request.NextAssigneeUserId);

            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/get-workflow-by-entity")]
    public async Task<IActionResult> GetWorkflowByEntity(Guid id)
    {
        var result = await _dossierService.GetWorkflowStatusByEntityAsync(id.ToString());
        return Ok(result);
    }

    [HttpGet("{id:guid}/get-workflow-history")]
    public async Task<IActionResult> GetWorkflowHistory(Guid id)
    {
        var history = await _dossierService.GetWorkflowHistoryAsync(id);
        return Ok(history);
    }

    [HttpGet("get-workflow-definition/{definitionId:guid}")]
    public async Task<IActionResult> GetWorkflowDefinition(Guid definitionId)
    {
        var def = await _dossierService.GetWorkflowDefinitionAsync(definitionId);
        if (def == null) return NotFound(new { message = $"Không tìm thấy định nghĩa quy trình với ID = {definitionId}" });
        return Ok(def);
    }

    [HttpGet("get-my-tasks")]
    public async Task<IActionResult> GetMyTasks([FromQuery] Guid? instanceId = null)
    {
        var userRoles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
        var isAdmin = User.IsInRole("ADMIN") || userRoles.Contains("ADMIN");
        var tasks = await _dossierService.GetMyTasksAsync(userRoles, isAdmin, UserId, instanceId);
        return Ok(tasks);
    }
}

/// <summary>Request model cho chuyển bước workflow</summary>
public class MoveWorkflowDossierRequest
{
    public string NextNodeId { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? NextAssigneeUserId { get; set; }
}
