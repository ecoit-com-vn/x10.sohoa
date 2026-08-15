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
}
