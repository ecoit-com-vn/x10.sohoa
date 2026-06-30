using Microsoft.AspNetCore.Mvc;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API NỘI BỘ đồng bộ trạng thái workflow hồ sơ từ WorkflowService.
/// - Đặt ngoài tiền tố "/api/v1/dossiers/..." nên KHÔNG có route ở ApiGateway ⇒ không expose ra Internet.
/// - [BypassDynamicPermission]: không kiểm quyền người dùng cuối (gọi service-to-service).
/// - Phòng thủ chiều sâu: bắt buộc khớp shared-secret header "X-Internal-Token".
/// Không gắn [Authorize] vì WorkflowService gọi bằng service token, không phải JWT người dùng.
/// </summary>
[ApiController]
[Route("internal/v1/dossiers")]
[BypassDynamicPermission]
public class InternalDossierController : ControllerBase
{
    private readonly IDossierService _dossierService;
    private readonly IConfiguration _configuration;

    public InternalDossierController(IDossierService dossierService, IConfiguration configuration)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [HttpPut("{id:guid}/workflow-state")]
    public async Task<IActionResult> UpdateInternalWorkflowState(
        Guid id,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] UpdateInternalWorkflowStateDto dto)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
            return StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên EquipmentService." });

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Token nội bộ không hợp lệ." });

        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            await _dossierService.UpdateWorkflowStateInternalAsync(id, dto);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInternalDossier(
        Guid id,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
            return StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên EquipmentService." });

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Token nội bộ không hợp lệ." });

        try
        {
            var dossier = await _dossierService.GetDetailByIdAsync(id);
            if (dossier == null) return NotFound(new { message = "Không tìm thấy hồ sơ." });
            return Ok(new { status = dossier.StatusCode, statusId = dossier.StatusId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
