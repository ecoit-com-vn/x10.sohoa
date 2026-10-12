using System.Text;
using EvnHanoi.SyncService.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// Lỗi đồng bộ PMIS có chủ (PMIS_SYNC_ERROR): mỗi bản ghi lỗi 1 dòng theo (loại, mã PMIS, mã lỗi), trạng thái OPEN/QUARANTINED/RESOLVED.
/// Lỗi do dữ liệu nguồn (vd. thiếu cấp điện áp) sau nhiều lần liên tiếp bị CÁCH LY — không thử lại mỗi lượt; admin xem tại đây và bấm "thử lại".
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/sync/errors")]
public class SyncErrorController : ControllerBase
{
    private readonly IPmisSyncErrorRepository _repository;

    public SyncErrorController(IPmisSyncErrorRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Đếm theo (loại, mã lỗi, trạng thái).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary() => Ok(await _repository.GetSummaryAsync());

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? kind, [FromQuery] string? errorCode, [FromQuery] string? state, [FromQuery] string? keyword,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (items, total) = await _repository.ListAsync(kind, errorCode, state, keyword, page, pageSize);
        return Ok(new { items, total, page = Math.Max(1, page), pageSize = Math.Clamp(pageSize, 1, 500) });
    }

    /// <summary>Mở lại (thử lại ngay ở lượt sau) các dòng đang cách ly khớp bộ lọc. Ít nhất 1 bộ lọc là bắt buộc để tránh mở lại hàng loạt vô ý.</summary>
    [HttpPost("retry")]
    public async Task<IActionResult> Retry([FromQuery] string? kind, [FromQuery] string? errorCode, [FromQuery] string? pmisCode)
    {
        if (string.IsNullOrWhiteSpace(kind) && string.IsNullOrWhiteSpace(errorCode) && string.IsNullOrWhiteSpace(pmisCode))
            return BadRequest(new { message = "Cần ít nhất 1 bộ lọc (kind, errorCode hoặc pmisCode)." });

        var reopened = await _repository.RetryAsync(kind, errorCode, pmisCode);
        return Ok(new { reopened });
    }

    /// <summary>Xuất CSV (tối đa 50.000 dòng) theo bộ lọc.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? kind, [FromQuery] string? errorCode, [FromQuery] string? state, [FromQuery] string? keyword)
    {
        var sb = new StringBuilder("﻿Loai,MaPMIS,MaLoi,TrangThai,SoLan,LanDau,LanCuoi,HanThuLai,ThongBao\r\n");
        const int pageSize = 500;
        for (var page = 1; page <= 100; page++)
        {
            var (items, _) = await _repository.ListAsync(kind, errorCode, state, keyword, page, pageSize);
            foreach (var r in items)
                sb.Append(Csv(r.EntityKind)).Append(',').Append(Csv(r.PmisCode)).Append(',').Append(Csv(r.ErrorCode)).Append(',').Append(Csv(r.State)).Append(',')
                  .Append(r.Occurrences).Append(',').Append(r.FirstSeenAt.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(r.LastSeenAt.ToString("yyyy-MM-dd HH:mm:ss")).Append(',')
                  .Append(r.NextRetryAt?.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(Csv(r.Message)).Append("\r\n");
            if (items.Count < pageSize) break;
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", $"pmis-sync-errors-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // Chống CSV injection: ô bắt đầu bằng = + - @ sẽ bị Excel hiểu là công thức.
        if (value[0] is '=' or '+' or '-' or '@') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    }
}
