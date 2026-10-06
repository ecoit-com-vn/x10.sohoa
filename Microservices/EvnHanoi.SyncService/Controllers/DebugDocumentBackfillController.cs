using System.Security.Cryptography;
using System.Text;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// Backfill: đồng bộ lại DANH SÁCH tài liệu PMIS (API 8/9) để lấy link file mới, CHỈ cho các Trạm/Đường dây
/// còn tài liệu chưa có file (PENDING/FAILED/NO_URL, gồm cả tài liệu của thiết bị con) — thay vì chờ lượt
/// đồng bộ định kỳ quay hết ~40.000 owner (mỗi lượt chỉ 2.000 owner, 2 giờ/lượt → 1–2 ngày). Dòng chưa có
/// file mà link đổi → tự về PENDING + FILE_ATTEMPTS=0 (EquipmentService.UpdateFileSourceAsync), job
/// PmisDocumentFileDownloadJob tải tiếp. Tuần tự từng owner, ngân sách 2.000 owner/scope được reset bằng cách
/// tạo scope mới mỗi <see cref="OwnersPerScope"/> owner.
///
/// Cùng cơ chế bảo vệ với DebugSqlController: mã bí mật "DebugSql:SecretKey" (header X-Debug-Sql-Secret) +
/// cờ "DebugSql:AllowExecute=true" (mặc định TẮT, thiếu thì 503; GET xem tiến độ chỉ cần mã bí mật). Route
/// dưới "api/v1/sync/..." để đi qua ApiGateway. Chạy NỀN (vượt timeout gateway): POST bắt đầu, GET xem tiến
/// độ. Lỗi từng owner chỉ đếm, không dừng cả lượt. Ghi SYNC_HISTORY (MANUAL, CreatedBy=DEBUG_BACKFILL) mỗi
/// loại Trạm/Đường dây; chi tiết chỉ lưu dòng Warning/Failed để khỏi làm phình bảng.
/// </summary>
[ApiController]
[Route("api/v1/sync/debug-sql/backfill-documents")]
[BypassDynamicPermission]
public class DebugDocumentBackfillController : ControllerBase
{
    private const int OwnersPerScope = 1500; // dưới trần 2.000 owner/scope của PmisSyncExecutionService

    private static readonly object StateLock = new();
    private static BackfillState _state = new();

    private sealed class BackfillState
    {
        public string Status { get; set; } = "IDLE"; // IDLE | RUNNING | DONE | FAILED
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public int TotalOwners { get; set; }
        public int ProcessedOwners { get; set; }
        public int FailedOwners { get; set; }
        public int Warnings { get; set; }
        public string? LastError { get; set; }
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DebugDocumentBackfillController> _logger;

    public DebugDocumentBackfillController(
        IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DebugDocumentBackfillController> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <param name="infraTypeId">1 = chỉ Trạm, 2 = chỉ Đường dây; bỏ trống = cả hai.</param>
    /// <param name="limit">Giới hạn số owner (thử nghiệm nhỏ trước khi chạy hết); bỏ trống = tất cả.</param>
    [HttpPost]
    public IActionResult Start(
        [FromHeader(Name = "X-Debug-Sql-Secret")] string? secret,
        [FromQuery] int? infraTypeId,
        [FromQuery] int? limit)
    {
        if (!ValidateAccess(secret, requireAllowExecute: true, out var error)) return error!;

        lock (StateLock)
        {
            if (_state.Status == "RUNNING")
                return Conflict(new { message = "Đang có lượt backfill chạy.", state = Snapshot() });

            _state = new BackfillState { Status = "RUNNING", StartedAtUtc = DateTime.UtcNow };
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogWarning("DebugDocumentBackfillController: bắt đầu backfill tài liệu từ {RemoteIp}, infraTypeId={InfraTypeId}, limit={Limit}",
            remoteIp, infraTypeId, limit);

        _ = Task.Run(() => RunAsync(infraTypeId, limit));
        return Accepted(new { message = "Đã bắt đầu backfill nền. GET cùng đường dẫn để xem tiến độ.", state = Snapshot() });
    }

    [HttpGet]
    public IActionResult GetStatus([FromHeader(Name = "X-Debug-Sql-Secret")] string? secret)
    {
        if (!ValidateAccess(secret, requireAllowExecute: false, out var error)) return error!;
        return Ok(Snapshot());
    }

    /// <summary>Chẩn đoán (chỉ đọc, chỉ cần mã bí mật): gọi API danh sách tài liệu của PMIS cho 1 Trạm/Đường dây và
    /// trả NGUYÊN VĂN JSON PMIS — để biết PMIS thực sự trả trường link file nào/giá trị gì (hệ thống không lưu
    /// phản hồi thô). <paramref name="infraTypeId"/>: 1 = Trạm (API 8), 2 = Đường dây (API 9).</summary>
    [HttpGet("peek")]
    public async Task<IActionResult> Peek(
        [FromHeader(Name = "X-Debug-Sql-Secret")] string? secret,
        [FromQuery] string ownerCode,
        [FromQuery] int infraTypeId = 2,
        [FromQuery] int take = 3)
    {
        if (!ValidateAccess(secret, requireAllowExecute: false, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(ownerCode)) return BadRequest(new { message = "Thiếu ownerCode (mã PMIS Trạm/Đường dây)." });

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IPmisClient>();
            var raw = await client.PeekDocumentsRawAsync(infraTypeId == 1, ownerCode, Math.Clamp(take, 1, 10));
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            return Ok(new { ownerCode, infraTypeId, pmisResponse = doc.RootElement.Clone() });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DebugDocumentBackfillController: peek PMIS lỗi cho {OwnerCode}", ownerCode);
            return StatusCode(502, new { message = "Gọi PMIS lỗi hoặc phản hồi không phải JSON.", detail = ex.Message });
        }
    }

    /// <summary>Chẩn đoán (chỉ đọc, chỉ cần mã bí mật): gọi API TaiFileTaiLieu cho 1 mã tài liệu đúng như job tải file
    /// (gateway + header cấu hình) và trả mã HTTP, loại nội dung, kích thước, nhận dạng định dạng (PDF/ảnh/JSON/
    /// base64) + phần đầu nội dung. KHÔNG lưu file, KHÔNG sửa DB.</summary>
    [HttpGet("peek-file")]
    public async Task<IActionResult> PeekFile(
        [FromHeader(Name = "X-Debug-Sql-Secret")] string? secret,
        [FromQuery] string maTaiLieu,
        [FromQuery] int infraTypeId = 2)
    {
        if (!ValidateAccess(secret, requireAllowExecute: false, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(maTaiLieu)) return BadRequest(new { message = "Thiếu maTaiLieu." });

        using var scope = _scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPmisClient>();
        var probe = await client.ProbeDocumentFileAsync(maTaiLieu, infraTypeId == 1 ? "SUBSTATION_DOCUMENT_LIST" : "LINE_DOCUMENT_LIST");
        return Ok(probe);
    }

    private async Task RunAsync(int? infraTypeId, int? limit)
    {
        var histories = new Dictionary<int, (string? Id, int Total, int Success, int Failed)>();
        var runFailed = false;
        try
        {
            List<SyncedInfrastructurePmisCode> owners;
            using (var scope = _scopeFactory.CreateScope())
            {
                owners = await scope.ServiceProvider.GetRequiredService<IEquipmentServiceClient>().GetPendingDocumentOwnersAsync();
            }

            owners = owners
                .Where(o => infraTypeId == null || o.InfraTypeId == infraTypeId)
                .Take(limit ?? int.MaxValue)
                .ToList();
            lock (StateLock) _state.TotalOwners = owners.Count;

            var processedInScope = 0;
            var scope2 = _scopeFactory.CreateScope();
            try
            {
                foreach (var owner in owners)
                {
                    if (processedInScope >= OwnersPerScope)
                    {
                        scope2.Dispose();
                        scope2 = _scopeFactory.CreateScope();
                        processedInScope = 0;
                    }

                    processedInScope++;
                    var sp = scope2.ServiceProvider;
                    var historyRepo = sp.GetRequiredService<ISyncHistoryRepository>();

                    if (!histories.TryGetValue(owner.InfraTypeId, out var history))
                    {
                        // Tạo SYNC_HISTORY lỗi (vd. thiếu dòng SYNC_CONFIG) KHÔNG được dừng cả lượt: ghi nhận lỗi,
                        // bỏ qua các owner của loại này (không có SyncHistoryId hợp lệ để gắn vào tài liệu) và
                        // tiếp tục loại còn lại.
                        string? id = null;
                        try
                        {
                            var objectType = owner.InfraTypeId == 1 ? SyncObjectType.Substation : SyncObjectType.TransmissionLine;
                            var config = await sp.GetRequiredService<ISyncConfigRepository>().GetByObjectTypeAsync(objectType);
                            if (config == null) throw new InvalidOperationException($"Thiếu dòng SYNC_CONFIG cho {objectType}.");
                            id = await historyRepo.CreateAsync(new SyncHistory
                            {
                                SyncConfigId = config.Id,
                                ObjectType = objectType,
                                SyncType = SyncType.Manual,
                                StartTime = DateTime.UtcNow,
                                Status = SyncHistoryStatus.Running,
                                CreatedBy = "DEBUG_BACKFILL",
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "DebugDocumentBackfillController: không tạo được SYNC_HISTORY cho infraTypeId={InfraTypeId}", owner.InfraTypeId);
                            lock (StateLock) _state.LastError = $"Không tạo được lịch sử cho loại {owner.InfraTypeId}: {ex.Message}";
                        }

                        history = (id, 0, 0, 0);
                    }

                    if (history.Id == null)
                    {
                        histories[owner.InfraTypeId] = (null, history.Total + 1, 0, history.Failed + 1);
                        lock (StateLock)
                        {
                            _state.FailedOwners++;
                            _state.ProcessedOwners++;
                        }

                        continue;
                    }

                    var ok = true;
                    try
                    {
                        var exec = sp.GetRequiredService<IPmisSyncExecutionService>();
                        var (warnings, details) = await exec.SyncDocumentsForInfrastructureOwnerAsync(owner.PmisCode, owner.InfraTypeId, history.Id!);
                        var problems = details.Where(d => d.Status != SyncDetailStatus.Success).ToList();
                        if (problems.Count > 0) await historyRepo.InsertDetailsAsync(problems);
                        lock (StateLock) _state.Warnings += warnings;
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        _logger.LogWarning(ex, "DebugDocumentBackfillController: lỗi backfill owner {PmisCode}", owner.PmisCode);
                        lock (StateLock)
                        {
                            _state.FailedOwners++;
                            _state.LastError = $"{owner.PmisCode}: {ex.Message}";
                        }
                    }

                    histories[owner.InfraTypeId] = (history.Id, history.Total + 1, history.Success + (ok ? 1 : 0), history.Failed + (ok ? 0 : 1));
                    lock (StateLock) _state.ProcessedOwners++;
                }
            }
            finally
            {
                scope2.Dispose();
            }

            lock (StateLock)
            {
                _state.Status = "DONE";
                _state.FinishedAtUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            runFailed = true;
            _logger.LogError(ex, "DebugDocumentBackfillController: backfill thất bại");
            lock (StateLock)
            {
                _state.Status = "FAILED";
                _state.FinishedAtUtc = DateTime.UtcNow;
                _state.LastError = ex.Message;
            }
        }
        finally
        {
            await CompleteHistoriesAsync(histories, runFailed);
        }
    }

    private async Task CompleteHistoriesAsync(Dictionary<int, (string? Id, int Total, int Success, int Failed)> histories, bool runFailed)
    {
        if (histories.Count == 0) return;
        int totalOwners, processed;
        lock (StateLock) { totalOwners = _state.TotalOwners; processed = _state.ProcessedOwners; }
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISyncHistoryRepository>();
            foreach (var h in histories.Values.Where(h => h.Id != null))
            {
                // Cả lượt thất bại giữa chừng → FAILED (không để SUCCESS dù chưa owner nào lỗi); có owner lỗi → WARNING.
                var status = runFailed ? SyncHistoryStatus.Failed
                    : h.Failed == 0 ? SyncHistoryStatus.Success
                    : SyncHistoryStatus.Warning;
                var parts = new List<string>();
                if (runFailed) parts.Add($"Lượt backfill dừng giữa chừng: đã xử lý {processed}/{totalOwners} owner (cả hai loại).");
                if (h.Failed > 0) parts.Add($"{h.Failed} owner lỗi khi backfill tài liệu.");
                await repo.CompleteAsync(h.Id!, status, h.Total, h.Success, h.Failed, parts.Count > 0 ? string.Join(" ", parts) : null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DebugDocumentBackfillController: không đóng được SYNC_HISTORY của backfill.");
        }
    }

    private object Snapshot()
    {
        lock (StateLock)
        {
            return new
            {
                _state.Status,
                _state.StartedAtUtc,
                _state.FinishedAtUtc,
                _state.TotalOwners,
                _state.ProcessedOwners,
                _state.FailedOwners,
                _state.Warnings,
                _state.LastError,
            };
        }
    }

    private bool ValidateAccess(string? provided, bool requireAllowExecute, out IActionResult? errorResult)
    {
        var expected = _configuration["DebugSql:SecretKey"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "DebugSql:SecretKey chưa được cấu hình trên SyncService." });
            return false;
        }

        if (string.IsNullOrEmpty(provided) || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)), SHA256.HashData(Encoding.UTF8.GetBytes(expected))))
        {
            errorResult = Unauthorized(new { message = "Mã bí mật không hợp lệ." });
            return false;
        }

        if (requireAllowExecute && !_configuration.GetValue<bool>("DebugSql:AllowExecute"))
        {
            errorResult = StatusCode(503, new { message = "Backfill đang TẮT. Đặt DebugSql__AllowExecute=true trên SyncService (restart) để bật tạm, xong nhớ tắt lại." });
            return false;
        }

        errorResult = null;
        return true;
    }
}
