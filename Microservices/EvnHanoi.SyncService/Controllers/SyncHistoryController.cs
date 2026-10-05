using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
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
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly ILogger<SyncHistoryController> _logger;

    public SyncHistoryController(
        ISyncHistoryRepository syncHistoryRepository,
        IEquipmentServiceClient equipmentServiceClient,
        ILogger<SyncHistoryController> logger)
    {
        _syncHistoryRepository = syncHistoryRepository;
        _equipmentServiceClient = equipmentServiceClient;
        _logger = logger;
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
    public async Task<IActionResult> GetItems(
        string historyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? recordKind = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, totalCount) = await _syncHistoryRepository.GetDetailsPagedAsync(historyId, page, pageSize, recordKind?.ToUpperInvariant());
        var views = items.Select(ToView).ToList();

        // Dòng tài liệu: "Thành công" của lượt đồng bộ chỉ nghĩa là đã lưu metadata + link; file thật do job nền tải
        // sau, nên gắn trạng thái tải file HIỆN TẠI (lỗi gần nhất, số lần thử) để người xem thấy sự thật. Lỗi gọi
        // EquipmentService chỉ làm mất phần bổ sung này, KHÔNG được làm hỏng danh sách lịch sử.
        var codes = views.Where(v => v.RecordKind == SyncRecordKind.Document && !string.IsNullOrWhiteSpace(v.SourceCode))
            .Select(v => v.SourceCode!).Distinct().ToList();
        if (codes.Count > 0)
        {
            try
            {
                var statuses = (await _equipmentServiceClient.GetDocumentFileStatusAsync(codes))
                    .GroupBy(x => x.PmisDocumentCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var v in views.Where(v => v.SourceCode != null && statuses.ContainsKey(v.SourceCode)))
                {
                    var st = statuses[v.SourceCode!];
                    v.FileStatus = st.FileStatus;
                    v.FileAttempts = st.FileAttempts;
                    v.FileLastError = st.FileLastError;
                    v.HasFile = st.HasFile;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SyncHistoryController: không lấy được trạng thái tải file cho lịch sử {HistoryId}.", historyId);
            }
        }

        return Ok(new { items = views, totalCount });
    }

    private static SyncHistoryDetailView ToView(SyncHistoryDetail d) => new()
    {
        Id = d.Id,
        SyncHistoryId = d.SyncHistoryId,
        SourceId = d.SourceId,
        SourceCode = d.SourceCode,
        SourceName = d.SourceName,
        TargetId = d.TargetId,
        ActionType = d.ActionType,
        Status = d.Status,
        DataContent = d.DataContent,
        ErrorMessage = d.ErrorMessage,
        SyncTime = d.SyncTime,
        RecordKind = d.RecordKind,
    };

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
