using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Elastic.Clients.Elasticsearch;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Models;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

/// <summary>
/// Công cụ vận hành: dựng lại chỉ mục Elasticsearch "equipments" từ bảng Equipments mà KHÔNG cần restart pod
/// (khởi động lại không giúp — bootstrap lúc khởi động bỏ qua khi index đã có dữ liệu, xem
/// ElasticsearchSetupService). Dùng sau các migration khôi phục hàng loạt (vd. Migration0073 khôi phục
/// ~179.000 thiết bị "sinh ra đã là hồn ma" — DB đã đúng nhưng ES chưa biết).
///
/// Cùng cơ chế bảo vệ với DebugSqlController của SyncService: không JWT ([BypassDynamicPermission]), bắt buộc
/// mã bí mật "DebugSql:SecretKey" (header X-Debug-Sql-Secret) và cờ "DebugSql:AllowExecute=true" (mặc định TẮT,
/// thiếu thì 503). Route nằm dưới "api/v1/search/..." để đi được qua ApiGateway (search-route →
/// notification-cluster). Thao tác ghi vào ES nên bật cờ tạm thời rồi tắt lại.
///
/// Chạy NỀN (hàng trăm nghìn dòng, vượt timeout của gateway): POST bắt đầu, GET xem tiến độ. Chỉ upsert theo Id
/// (idempotent, chạy lại thoải mái) các thiết bị "sống" (IsDeleted=0 AND StatusTransition IS NULL) bằng Bulk API.
/// KHÔNG xoá tài liệu cũ trong ES của thiết bị đã xoá/đã chuyển TBA.
/// </summary>
[ApiController]
[Route("api/v1/search/debug/reindex-equipments")]
[BypassDynamicPermission]
public class DebugIndexController : ControllerBase
{
    private const int BatchSize = 1000;
    private const string IndexName = "equipments";

    // Trạng thái của lượt chạy hiện tại/gần nhất — 1 pod, 1 lượt tại 1 thời điểm.
    private static readonly object StateLock = new();
    private static ReindexState _state = new();

    private sealed class ReindexState
    {
        public string Status { get; set; } = "IDLE"; // IDLE | RUNNING | DONE | FAILED
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public long Total { get; set; }
        public long Indexed { get; set; }
        public long FailedDocs { get; set; }
        public string? LastError { get; set; }
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DebugIndexController> _logger;

    public DebugIndexController(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DebugIndexController> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Start([FromHeader(Name = "X-Debug-Sql-Secret")] string? secret)
    {
        if (!ValidateAccess(secret, requireAllowExecute: true, out var error)) return error!;

        lock (StateLock)
        {
            if (_state.Status == "RUNNING")
            {
                return Conflict(new { message = "Đang có lượt reindex chạy.", state = Snapshot() });
            }

            _state = new ReindexState { Status = "RUNNING", StartedAtUtc = DateTime.UtcNow };
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogWarning("DebugIndexController: bắt đầu reindex thiết bị lên ES từ {RemoteIp}", remoteIp);

        _ = Task.Run(RunReindexAsync);
        return Accepted(new { message = "Đã bắt đầu reindex nền. GET cùng đường dẫn để xem tiến độ.", state = Snapshot() });
    }

    [HttpGet]
    public IActionResult GetStatus([FromHeader(Name = "X-Debug-Sql-Secret")] string? secret)
    {
        if (!ValidateAccess(secret, requireAllowExecute: false, out var error)) return error!;
        return Ok(Snapshot());
    }

    private async Task RunReindexAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
            var client = scope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
            connection.EnsureOpen();

            const string whereLive = "WHERE IsDeleted = 0 AND StatusTransition IS NULL";
            var total = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM Equipments {whereLive}", commandTimeout: 300);
            lock (StateLock) _state.Total = total;

            var dbConnection = connection as System.Data.Common.DbConnection
                ?? throw new InvalidOperationException("IDbConnection đăng ký không phải DbConnection.");
            var rows = dbConnection.QueryUnbufferedAsync<Equipment>(
                $"SELECT Id, Name, Code, '' AS Description, SerialNumber AS Type, '1' AS Status FROM Equipments {whereLive}",
                commandTimeout: 1800);

            var batch = new List<Equipment>(BatchSize);
            await foreach (var row in rows)
            {
                batch.Add(row);
                if (batch.Count >= BatchSize)
                {
                    await FlushAsync(client, batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0) await FlushAsync(client, batch);

            lock (StateLock)
            {
                _state.Status = "DONE";
                _state.FinishedAtUtc = DateTime.UtcNow;
            }

            _logger.LogWarning("DebugIndexController: reindex xong — {Indexed}/{Total} thiết bị, {Failed} lỗi", _state.Indexed, _state.Total, _state.FailedDocs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DebugIndexController: reindex thiết bị thất bại");
            lock (StateLock)
            {
                _state.Status = "FAILED";
                _state.FinishedAtUtc = DateTime.UtcNow;
                _state.LastError = ex.Message;
            }
        }
    }

    private static async Task FlushAsync(ElasticsearchClient client, List<Equipment> batch)
    {
        var response = await client.BulkAsync(b => b
            .Index(IndexName)
            .IndexMany(batch, (op, eq) => op.Id(eq.Id)));

        // Cả request lỗi (HTTP không thành công) → coi cả lô lỗi; request OK nhưng có item lỗi → đếm đúng item.
        var requestOk = response.ApiCallDetails.HasSuccessfulStatusCode;
        var failed = requestOk ? response.Items.Count(i => i.Error != null) : batch.Count;
        lock (StateLock)
        {
            _state.Indexed += batch.Count - failed;
            _state.FailedDocs += failed;
            if (failed > 0) _state.LastError = requestOk
                ? response.Items.FirstOrDefault(i => i.Error != null)?.Error?.Reason
                : response.DebugInformation;
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
                _state.Total,
                _state.Indexed,
                _state.FailedDocs,
                _state.LastError,
            };
        }
    }

    private bool ValidateAccess(string? provided, bool requireAllowExecute, out IActionResult? errorResult)
    {
        var expected = _configuration["DebugSql:SecretKey"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "DebugSql:SecretKey chưa được cấu hình trên NotificationService." });
            return false;
        }

        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, expected))
        {
            errorResult = Unauthorized(new { message = "Mã bí mật không hợp lệ." });
            return false;
        }

        if (requireAllowExecute && !_configuration.GetValue<bool>("DebugSql:AllowExecute"))
        {
            errorResult = StatusCode(503, new
            {
                message = "Reindex đang TẮT. Đặt DebugSql__AllowExecute=true trên NotificationService (restart) để bật tạm, xong nhớ tắt lại.",
            });
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool FixedTimeEquals(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
