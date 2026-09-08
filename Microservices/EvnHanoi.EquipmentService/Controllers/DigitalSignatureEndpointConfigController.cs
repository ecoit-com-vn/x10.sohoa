using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Cấu hình URL + trạng thái cho 3 API tích hợp ký số ngoài (đúng số lượng cố định — chỉ sửa,
/// không thêm/xoá dòng), theo cùng pattern PmisEndpointConfigController (SyncService).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/digital-signature/endpoint-config")]
public class DigitalSignatureEndpointConfigController : ControllerBase
{
    private readonly IDigitalSignatureEndpointConfigRepository _repository;

    public DigitalSignatureEndpointConfigController(IDigitalSignatureEndpointConfigRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _repository.GetAllAsync();
        return Ok(items);
    }

    [HttpPut("{apiCode}")]
    public async Task<IActionResult> Update(string apiCode, [FromBody] UpdateDigitalSignatureEndpointConfigRequest request)
    {
        var existing = await _repository.GetByApiCodeAsync(apiCode);
        if (existing == null) return NotFound(new { message = "Không tìm thấy API ký số cần cập nhật." });

        var modifiedBy = CurrentUserName();
        var updated = await _repository.UpdateAsync(apiCode, request, modifiedBy);
        if (!updated)
        {
            return Conflict(new
            {
                message = "Dữ liệu đã được người khác cập nhật, vui lòng tải lại trang trước khi lưu tiếp."
            });
        }

        return NoContent();
    }

    private string? CurrentUserName() =>
        User.FindFirstValue("full_name") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
