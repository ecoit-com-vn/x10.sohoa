using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Ánh xạ loại thiết bị PMIS ↔ loại thiết bị hệ thống (theo cấp lưới điện). Admin tự cấu hình —
/// thiếu ánh xạ thì đồng bộ thiết bị từ PMIS sẽ báo lỗi ngay ở thiết bị đó (xem
/// EquipmentRepository.UpsertFromPmisAsync).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/pmis-equipment-type-mapping")]
public class PmisEquipmentTypeMappingController : ControllerBase
{
    private readonly IPmisEquipmentTypeMappingRepository _repository;

    public PmisEquipmentTypeMappingController(IPmisEquipmentTypeMappingRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _repository.GetAllAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePmisEquipmentTypeMappingRequest request)
    {
        if (!Validate(request, out var error)) return BadRequest(new { message = error });

        try
        {
            var id = await _repository.CreateAsync(request, CurrentUserName());
            return Ok(new { id });
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Mã loại thiết bị PMIS này đã được ánh xạ ở cấp điện áp đã chọn." });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SavePmisEquipmentTypeMappingRequest request)
    {
        if (!Validate(request, out var error)) return BadRequest(new { message = error });
        if (request.RowVersion is null) return BadRequest(new { message = "Thiếu phiên bản dữ liệu (rowVersion)." });

        try
        {
            var updated = await _repository.UpdateAsync(id, request, CurrentUserName());
            if (!updated)
                return Conflict(new { message = "Dữ liệu đã được người khác cập nhật, vui lòng tải lại danh sách." });

            return NoContent();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Mã loại thiết bị PMIS này đã được ánh xạ ở cấp điện áp đã chọn." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentUserName());
        if (!deleted) return NotFound(new { message = "Không tìm thấy ánh xạ cần xoá." });

        return NoContent();
    }

    private static bool Validate(SavePmisEquipmentTypeMappingRequest request, out string? error)
    {
        if (string.IsNullOrWhiteSpace(request.PmisMaLoaiTB))
        {
            error = "Mã loại thiết bị PMIS là bắt buộc.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.EquipmentTypeId))
        {
            error = "Loại thiết bị hệ thống là bắt buộc.";
            return false;
        }

        if (request.GridTypeId <= 0)
        {
            error = "Cấp điện áp là bắt buộc.";
            return false;
        }

        error = null;
        return true;
    }

    private string? CurrentUserName() =>
        User.FindFirstValue("full_name")
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
