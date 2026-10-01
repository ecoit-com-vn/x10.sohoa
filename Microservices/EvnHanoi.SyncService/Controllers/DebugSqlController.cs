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
///
/// Ngoài /select (chỉ đọc) còn có /execute để chạy script migration thủ công — mặc định TẮT, cần thêm
/// DebugSql__AllowExecute=true; xem doc của RunScript.
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

    private const int MaxScriptLength = 200_000;
    private const int ScriptCommandTimeoutSeconds = 900;
    private const int MaxOutputLines = 2000;

    // Lưới an toàn chống thao tác nhầm của /execute — không phải ranh giới bảo mật (xem doc của RunScript).
    private static readonly Regex BlockedScriptPattern = new(
        @"\b(DROP\s+(USER|TABLESPACE|DATABASE)|ALTER\s+(SYSTEM|USER|DATABASE)|GRANT|REVOKE|SHUTDOWN|UTL_FILE|UTL_HTTP|UTL_TCP|UTL_SMTP|DBMS_SCHEDULER|DBMS_JOB|DBMS_PIPE|DBMS_SQL)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SqlPlusDirectivePattern = new(
        @"^\s*(SET\s+(SERVEROUTPUT|DEFINE|ECHO|FEEDBACK|TIMING|LINESIZE|PAGESIZE|VERIFY|SQLBLANKLINES|TERMOUT|TRIMSPOOL|HEADING)\b|PROMPT\b|SPOOL\b|WHENEVER\s+(SQLERROR|OSERROR)\b|EXIT\b|QUIT\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PlSqlStartPattern = new(
        @"^(DECLARE|BEGIN|CREATE\s+(OR\s+REPLACE\s+)?(PROCEDURE|FUNCTION|PACKAGE|TRIGGER|TYPE)\b)",
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
                    new { MaxRows = maxRows },
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

    public sealed class DebugSqlExecuteRequest
    {
        /// <summary>Nội dung script (nguyên file .sql thủ công): nhiều câu lệnh / khối PL/SQL, ngăn cách bằng dòng "/" hoặc ";".</summary>
        public string Script { get; set; } = string.Empty;

        /// <summary>true = chỉ tách + kiểm tra script rồi trả danh sách lệnh, KHÔNG chạy gì.</summary>
        public bool DryRun { get; set; }

        /// <summary>Phải đúng chữ "EXECUTE" thì mới chạy thật (chống gọi nhầm).</summary>
        public string? Confirm { get; set; }
    }

    private sealed record ScriptUnit(string Kind, string Sql);

    /// <summary>
    /// Chạy script migration thủ công (DDL/DML/PL/SQL) — dành cho các file trong Migrations/Manual khi không có
    /// sqlplus/SSH tới DB. KHÁC /select: ghi được dữ liệu và đổi schema, nên có thêm 3 lớp chặn:
    /// (1) mặc định TẮT — chỉ chạy khi cấu hình "DebugSql:AllowExecute=true" (biến môi trường
    /// DebugSql__AllowExecute), nên bật tạm lúc cần rồi tắt lại; (2) phải khớp mã bí mật như /select;
    /// (3) body phải có confirm="EXECUTE" (hoặc dryRun=true để chỉ xem trước các lệnh đã tách).
    /// Script chạy tuần tự trên 1 kết nối, DỪNG ở lệnh lỗi đầu tiên (các lệnh trước đó đã commit/DDL không
    /// rollback được), trả về DBMS_OUTPUT từng lệnh. Danh sách chặn bên dưới chỉ là lưới an toàn chống thao tác
    /// nhầm (DROP USER, GRANT, ...), KHÔNG phải ranh giới bảo mật — ranh giới thật là mã bí mật + cờ AllowExecute.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> RunScript(
        [FromHeader(Name = "X-Debug-Sql-Secret")] string? secret,
        [FromBody] DebugSqlExecuteRequest request)
    {
        if (!ValidateSecret(secret, out var secretError)) return secretError!;

        if (!_configuration.GetValue<bool>("DebugSql:AllowExecute"))
        {
            return StatusCode(503, new
            {
                message = "Chạy script đang TẮT. Đặt DebugSql__AllowExecute=true trên SyncService (restart) để bật tạm, chạy xong nhớ tắt lại.",
            });
        }

        if (string.IsNullOrWhiteSpace(request.Script))
        {
            return BadRequest(new { message = "Thiếu nội dung script (\"script\")." });
        }

        if (request.Script.Length > MaxScriptLength)
        {
            return BadRequest(new { message = $"Script quá dài (tối đa {MaxScriptLength} ký tự)." });
        }

        var units = SplitScript(request.Script);
        if (units.Count == 0)
        {
            return BadRequest(new { message = "Script không có câu lệnh nào chạy được (chỉ toàn comment/lệnh SQL*Plus?)." });
        }

        foreach (var unit in units)
        {
            var blocked = BlockedScriptPattern.Match(StripComments(unit.Sql));
            if (blocked.Success)
            {
                return BadRequest(new { message = $"Script chứa thao tác không được phép: {blocked.Value.ToUpperInvariant()}." });
            }
        }

        if (request.DryRun)
        {
            return Ok(new
            {
                dryRun = true,
                unitCount = units.Count,
                units = units.Select((u, i) => new
                {
                    index = i + 1,
                    kind = u.Kind,
                    preview = u.Sql.Length > 200 ? u.Sql[..200] + "..." : u.Sql,
                }),
            });
        }

        if (!string.Equals(request.Confirm, "EXECUTE", StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Thiếu xác nhận: đặt \"confirm\": \"EXECUTE\" để chạy thật (hoặc \"dryRun\": true để xem trước)." });
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var scriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Script)))[..16];
        _logger.LogWarning(
            "DebugSqlController: CHẠY SCRIPT từ {RemoteIp}, {UnitCount} lệnh, sha256={ScriptHash}, script={Script}",
            remoteIp, units.Count, scriptHash, request.Script);

        var results = new List<object>();
        try
        {
            _connection.EnsureOpen();
            await _connection.ExecuteAsync("BEGIN DBMS_OUTPUT.ENABLE(1000000); END;");

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var startedAt = DateTime.UtcNow;
                int? rowsAffected = null;
                string? error = null;

                try
                {
                    rowsAffected = await _connection.ExecuteAsync(unit.Sql, commandTimeout: ScriptCommandTimeoutSeconds);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    _logger.LogError(ex, "DebugSqlController: script {ScriptHash} lỗi ở lệnh {Index}/{Total}", scriptHash, i + 1, units.Count);
                }

                var output = await DrainOutputAsync();
                results.Add(new
                {
                    index = i + 1,
                    kind = unit.Kind,
                    // Với khối PL/SQL Oracle trả -1; số dòng thật nằm trong output (DBMS_OUTPUT).
                    rowsAffected,
                    elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds,
                    output,
                    error,
                });

                if (error != null)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = $"Dừng ở lệnh {i + 1}/{units.Count} vì lỗi — các lệnh trước đó đã được áp dụng.",
                        scriptHash,
                        results,
                    });
                }
            }

            return Ok(new { success = true, unitCount = units.Count, scriptHash, results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DebugSqlController: lỗi hạ tầng khi chạy script {ScriptHash}", scriptHash);
            return StatusCode(500, new { success = false, message = "Lỗi khi chạy script.", detail = ex.Message, results });
        }
    }

    private async Task<List<string>> DrainOutputAsync()
    {
        var lines = new List<string>();
        try
        {
            for (var i = 0; i < MaxOutputLines; i++)
            {
                var parameters = new DynamicParameters();
                parameters.Add("outLine", dbType: DbType.String, direction: ParameterDirection.Output, size: 32767);
                parameters.Add("outStatus", dbType: DbType.Int32, direction: ParameterDirection.Output);
                await _connection.ExecuteAsync("BEGIN DBMS_OUTPUT.GET_LINE(:outLine, :outStatus); END;", parameters);
                if (parameters.Get<int>("outStatus") != 0) break;
                lines.Add(parameters.Get<string>("outLine"));
            }
        }
        catch (Exception ex)
        {
            lines.Add($"(không đọc được DBMS_OUTPUT: {ex.Message})");
        }

        return lines;
    }

    /// <summary>
    /// Tách script kiểu SQL*Plus: dòng chỉ có "/" kết thúc 1 đơn vị; khối bắt đầu bằng DECLARE/BEGIN/CREATE
    /// PROCEDURE... là 1 lệnh nguyên khối; còn lại tách theo ";" (bỏ qua ";" trong chuỗi/comment). Bỏ các lệnh
    /// SQL*Plus (SET SERVEROUTPUT, PROMPT, SPOOL, WHENEVER, EXIT...) vì driver không hiểu chúng.
    /// </summary>
    private static List<ScriptUnit> SplitScript(string script)
    {
        var units = new List<ScriptUnit>();
        var chunk = new StringBuilder();

        void Flush()
        {
            AddChunk(units, chunk.ToString());
            chunk.Clear();
        }

        foreach (var line in script.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Trim() == "/")
            {
                Flush();
                continue;
            }

            if (SqlPlusDirectivePattern.IsMatch(line)) continue;
            chunk.Append(line).Append('\n');
        }

        Flush();
        return units;
    }

    private static void AddChunk(List<ScriptUnit> units, string chunk)
    {
        var withoutComments = StripComments(chunk).Trim();
        if (withoutComments.Length == 0) return;

        if (PlSqlStartPattern.IsMatch(withoutComments))
        {
            units.Add(new ScriptUnit("PLSQL", chunk.Trim()));
            return;
        }

        var current = new StringBuilder();
        var inString = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < chunk.Length; i++)
        {
            var c = chunk[i];
            var next = i + 1 < chunk.Length ? chunk[i + 1] : '\0';

            if (inLineComment)
            {
                current.Append(c);
                if (c == '\n') inLineComment = false;
                continue;
            }

            if (inBlockComment)
            {
                current.Append(c);
                if (c == '*' && next == '/') { current.Append(next); i++; inBlockComment = false; }
                continue;
            }

            if (inString)
            {
                current.Append(c);
                if (c == '\'') inString = false; // '' (nháy kép) tự bật lại ở vòng sau.
                continue;
            }

            if (c == '-' && next == '-') { inLineComment = true; current.Append(c); continue; }
            if (c == '/' && next == '*') { inBlockComment = true; current.Append(c); continue; }
            if (c == '\'') { inString = true; current.Append(c); continue; }

            if (c == ';')
            {
                AddSqlStatement(units, current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        AddSqlStatement(units, current.ToString());
    }

    private static void AddSqlStatement(List<ScriptUnit> units, string statement)
    {
        if (StripComments(statement).Trim().Length == 0) return;
        units.Add(new ScriptUnit("SQL", statement.Trim()));
    }

    /// <summary>Bỏ comment "--" và "/* */" (tôn trọng chuỗi '...') để kiểm tra từ khoá/độ rỗng không bị comment đánh lừa.</summary>
    private static string StripComments(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var inString = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inString)
            {
                sb.Append(c);
                if (c == '\'') inString = false;
                continue;
            }

            if (c == '\'') { inString = true; sb.Append(c); continue; }

            if (c == '-' && next == '-')
            {
                while (i < sql.Length && sql[i] != '\n') i++;
                sb.Append('\n');
                continue;
            }

            if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i++;
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
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

        // Bind variable Oracle BẮT BUỘC bắt đầu bằng chữ cái (giống quy tắc định danh thường) — ":__maxRows"
        // (2 dấu gạch dưới đầu) từng khiến MỌI câu SELECT lỗi "ORA-00911: invalid character", kể cả
        // "SELECT 1 FROM DUAL" đơn giản nhất — phát hiện thật khi test endpoint lần đầu qua ApiGateway
        // (2026-09-28), trước đó endpoint chưa từng được gọi thật với Oracle.
        wrappedSql = $"SELECT * FROM ({trimmed}) WHERE ROWNUM <= :MaxRows";
        error = null;
        return true;
    }
}
