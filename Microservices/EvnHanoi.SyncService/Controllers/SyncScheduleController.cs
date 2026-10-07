using System.Security.Claims;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>Module 1 — thiết lập lịch đồng bộ tự động theo từng đối tượng (Trạm/Đường dây/Thiết bị).</summary>
[Authorize]
[ApiController]
[Route("api/v1/sync/config")]
public class SyncScheduleController : ControllerBase
{
    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly IOptions<SyncScheduleOptions> _scheduleOptions;

    public SyncScheduleController(ISyncConfigRepository syncConfigRepository, IOptions<SyncScheduleOptions> scheduleOptions)
    {
        _syncConfigRepository = syncConfigRepository;
        _scheduleOptions = scheduleOptions;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _syncConfigRepository.GetAllAsync();
        return Ok(items);
    }

    [HttpPut("{objectType}")]
    public async Task<IActionResult> Update(string objectType, [FromBody] UpdateSyncConfigRequest request)
    {
        var normalizedType = objectType.ToUpperInvariant();
        if (!SyncObjectType.IsSchedulable(normalizedType))
            return BadRequest(new { message = "Đối tượng đồng bộ không hợp lệ." });

        if (request.FrequencyValue <= 0)
            return BadRequest(new { message = "Tần suất đồng bộ phải lớn hơn 0." });

        if (request.FrequencyUnit is not ("MINUTE" or "HOUR" or "DAY"))
            return BadRequest(new { message = "Đơn vị tần suất không hợp lệ (MINUTE/HOUR/DAY)." });

        // Tần suất tối thiểu (mặc định 2 giờ): thời gian chạy tối đa của 1 lượt = tần suất − đệm nên tần suất quá thấp
        // không đủ thời gian đồng bộ khối lượng PMIS thật.
        if (SyncRunBudget.IsBelowMinimum(request.FrequencyValue, request.FrequencyUnit, _scheduleOptions.Value))
        {
            var min = _scheduleOptions.Value.MinFrequencyMinutes;
            return BadRequest(new { message = $"Tần suất đồng bộ tối thiểu là {FormatMinutes(min)}." });
        }

        var existing = await _syncConfigRepository.GetByObjectTypeAsync(normalizedType);
        if (existing == null) return NotFound(new { message = "Không tìm thấy cấu hình lịch đồng bộ." });

        var updated = await _syncConfigRepository.UpdateAsync(normalizedType, request, CurrentUserName());
        if (!updated)
        {
            return Conflict(new
            {
                message = "Dữ liệu đã được người khác cập nhật, vui lòng tải lại trang trước khi lưu tiếp."
            });
        }

        return NoContent();
    }

    private static string FormatMinutes(int minutes) =>
        minutes % 60 == 0 ? $"{minutes / 60} giờ" : $"{minutes} phút";

    private string? CurrentUserName() =>
        User.FindFirstValue("full_name") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
