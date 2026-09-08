using EvnHanoi.SyncService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>Module 4 — lịch sử đồng bộ: thời gian + danh sách bản ghi đã đồng bộ tại từng lần chạy.</summary>
[Authorize]
[ApiController]
[Route("api/v1/sync/history")]
public class SyncHistoryController : ControllerBase
{
    private readonly ISyncHistoryRepository _syncHistoryRepository;

    public SyncHistoryController(ISyncHistoryRepository syncHistoryRepository)
    {
        _syncHistoryRepository = syncHistoryRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? objectType, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, totalCount) = await _syncHistoryRepository.GetPagedAsync(objectType?.ToUpperInvariant(), page, pageSize);
        return Ok(new { items, totalCount });
    }

    [HttpGet("{historyId}/items")]
    public async Task<IActionResult> GetItems(string historyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, totalCount) = await _syncHistoryRepository.GetDetailsPagedAsync(historyId, page, pageSize);
        return Ok(new { items, totalCount });
    }

    /// <summary>Xoá thủ công Lịch sử đồng bộ (nút "Xoá lịch sử") — 4 chế độ, xem SyncHistoryRepository.DeleteAsync.</summary>
    [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup([FromBody] CleanupSyncHistoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectType))
            return BadRequest(new { message = "Thiếu đối tượng cần xoá lịch sử." });

        if (request.Mode == "DATE_RANGE" && (request.FromDate == null || request.ToDate == null))
            return BadRequest(new { message = "Vui lòng chọn đủ 'Từ ngày' và 'Đến ngày'." });

        try
        {
            var deletedCount = await _syncHistoryRepository.DeleteAsync(
                request.ObjectType.ToUpperInvariant(), request.Mode, request.FromDate, request.ToDate);
            return Ok(new { deletedCount });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class CleanupSyncHistoryRequest
{
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>"DATE_RANGE" | "KEEP_LAST_1_DAY" | "KEEP_LAST_7_DAYS" | "ALL".</summary>
    public string Mode { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
