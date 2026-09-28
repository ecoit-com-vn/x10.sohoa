using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// CHỈ DÙNG ĐỂ GỠ LỖI TRÊN STAGING/UAT — KHÔNG ĐƯỢC SET "DebugSql:SecretKey" Ở PRODUCTION.
/// Cho phép chạy 1 câu SELECT tùy ý để soi dữ liệu khi debug, không đi qua JWT/DynamicPermission
/// ([BypassDynamicPermission]) — thay vào đó bắt buộc khớp mã bí mật cấu hình ở "DebugSql:SecretKey"
/// (đọc qua biến môi trường DebugSql__SecretKey trên staging, KHÔNG commit giá trị thật vào
/// appsettings*.json). Chỉ chấp nhận đúng 1 câu SELECT/WITH, chặn các từ khóa DML/DDL, và luôn giới hạn
/// số dòng trả về bằng ROWNUM để tránh kéo cả bảng lớn.
///
/// Route đặt dưới "api/v1/sync/..." (khớp "sync-route" trong ApiGateway appsettings.json,
/// Path "/api/v1/sync/{**catch-all}") ĐỂ GỌI ĐƯỢC TỪ NGOÀI qua ApiGateway/ingress — trước đây route nằm
/// ở "internal/v1/debug-sql" (chỉ gọi được từ trong cluster) — đổi theo yêu cầu 2026-09-28 vì cần debug
/// dữ liệu PMIS sync trên môi trường không có kubectl/SSH. HỆ QUẢ BẢO MẬT: endpoint chạy SELECT tuỳ ý
/// lên DB thật giờ lộ ra internet công khai, chỉ còn được bảo vệ bởi mã bí mật (không JWT, không giới
/// hạn số lần thử) — do đó CHỈ ĐƯỢC set biến DebugSql__SecretKey trên UAT, KHÔNG BAO GIỜ set trên
/// production (thiếu biến này thì action luôn trả 503, an toàn mặc định).
/// </summary>
[ApiController]
[Route("api/v1/sync/debug-sql")]
[BypassDynamicPermission]
public class DebugSqlController : ControllerBase
{
    private const int DefaultMaxRows = 200;
    private const int HardMaxRows = 1000;
    private const int CommandTimeoutSeconds = 30;

    private static readonly Regex ForbiddenKeywordPattern = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|TRUNCATE|CREATE|GRANT|REVOKE|EXEC|EXECUTE|CALL|DECLARE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDbConnection _connection;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DebugSqlController> _logger;

    public DebugSqlController(IDbConnection connection, IConfiguration configuration, ILogger<DebugSqlController> logger)
    {
        _connection = connection;
        _configuration = configuration;
        _logger = logger;
    }

    public sealed class DebugSqlRequest
    {
        public string Sql { get; set; } = string.Empty;
        public int? MaxRows { get; set; }
    }

    [HttpPost("select")]
    public async Task<IActionResult> RunSelect(
        [FromHeader(Name = "X-Debug-Sql-Secret")] string? secret,
        [FromBody] DebugSqlRequest request)
    {
        if (!ValidateSecret(secret, out var secretError)) return secretError!;

        if (!TryPrepareSelectOnlySql(request.Sql, out var wrappedSql, out var validationError))
        {
            return BadRequest(new { message = validationError });
        }

        var maxRows = Math.Clamp(request.MaxRows ?? DefaultMaxRows, 1, HardMaxRows);
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogWarning(
            "DebugSqlController: chạy SELECT gỡ lỗi từ {RemoteIp}, maxRows={MaxRows}, sql={Sql}",
            remoteIp, maxRows, request.Sql);

        try
        {
            _connection.EnsureOpen();
            var startedAt = DateTime.UtcNow;
            var rows = (await _connection.QueryAsync(
                    wrappedSql,
                    new { __maxRows = maxRows },
                    commandTimeout: CommandTimeoutSeconds))
                .Select(row => (IDictionary<string, object>)row)
                .ToList();
            var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;

            return Ok(new
            {
                columns = rows.Count > 0 ? rows[0].Keys : Array.Empty<string>(),
                rowCount = rows.Count,
                elapsedMs,
                rows,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DebugSqlController: lỗi khi chạy SELECT gỡ lỗi từ {RemoteIp}", remoteIp);
            return StatusCode(500, new { message = "Lỗi khi thực thi câu SELECT.", detail = ex.Message });
        }
    }

    private bool ValidateSecret(string? provided, out IActionResult? errorResult)
    {
        var expected = _configuration["DebugSql:SecretKey"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "DebugSql:SecretKey chưa được cấu hình trên SyncService." });
            return false;
        }

        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, expected))
        {
            errorResult = Unauthorized(new { message = "Mã bí mật không hợp lệ." });
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private static bool TryPrepareSelectOnlySql(string? sql, out string wrappedSql, out string? error)
    {
        wrappedSql = string.Empty;

        if (string.IsNullOrWhiteSpace(sql))
        {
            error = "Thiếu nội dung câu SQL (\"sql\").";
            return false;
        }

        var trimmed = sql.Trim().TrimEnd(';').Trim();

        if (trimmed.Contains(';'))
        {
            error = "Chỉ được chạy đúng 1 câu lệnh — không cho phép nhiều câu lệnh ghép bằng \";\".";
            return false;
        }

        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            error = "Chỉ chấp nhận câu lệnh bắt đầu bằng SELECT hoặc WITH.";
            return false;
        }

        var forbiddenMatch = ForbiddenKeywordPattern.Match(trimmed);
        if (forbiddenMatch.Success)
        {
            error = $"Câu lệnh chứa từ khóa không được phép: {forbiddenMatch.Value.ToUpperInvariant()}.";
            return false;
        }

        wrappedSql = $"SELECT * FROM ({trimmed}) WHERE ROWNUM <= :__maxRows";
        error = null;
        return true;
    }
}
