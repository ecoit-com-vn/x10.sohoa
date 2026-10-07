using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Công cụ vận hành: xem và xoá file trong MinIO không cần truy cập console MinIO/kubectl — dùng khi cần soi hoặc dọn
/// file tài liệu PMIS đã tải về (vd. tài liệu bị gán nhầm thiết bị). Cùng cơ chế bảo vệ với DebugSqlController (SyncService):
/// không JWT ([BypassDynamicPermission]), bắt buộc mã bí mật "DebugSql:SecretKey" (header X-Debug-Sql-Secret). Thao tác XOÁ
/// cần thêm cờ "DebugSql:AllowExecute=true" (mặc định TẮT — thiếu thì 503) và body confirm="DELETE". Route dưới
/// "api/v1/pmis-documents/..." để đi qua ApiGateway (pmis-documents-route → equipment-cluster).
///
/// Phạm vi XOÁ cố ý hẹp: chỉ bucket tài liệu và object key bắt đầu bằng "pmis/" (file tài liệu PMIS) — không đụng hồ sơ
/// số hoá/dossier. Mặc định (resetDb=true) dòng PMIS_DOCUMENT trỏ tới object bị xoá được đặt lại về PENDING (ObjectKey/FileSize
/// NULL, FILE_ATTEMPTS=0) để job tải file tải lại; đặt resetDb=false nếu chỉ muốn xoá file. Nếu bucket bật versioning thì
/// xoá object chỉ tạo "delete marker" (các phiên bản cũ vẫn còn trong MinIO).
/// </summary>
[ApiController]
[Route("api/v1/pmis-documents/debug-minio")]
[BypassDynamicPermission]
public class DebugMinioController : ControllerBase
{
    private const string DeletablePrefix = "pmis/";
    private const int DefaultMaxKeys = 200;
    private const int HardMaxKeys = 2000;
    private const int SummaryScanLimit = 500_000;
    private const int HardMaxDelete = 10_000;

    private readonly IMinioClient _minio;
    private readonly IFileStorageService _fileStorage;
    private readonly IDbConnection _connection;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DebugMinioController> _logger;

    public DebugMinioController(
        IMinioClient minio, IFileStorageService fileStorage, IDbConnection connection,
        IConfiguration configuration, ILogger<DebugMinioController> logger)
    {
        _minio = minio;
        _fileStorage = fileStorage;
        _connection = connection;
        _configuration = configuration;
        _logger = logger;
    }

    public sealed class DeleteRequest
    {
        /// <summary>Danh sách object key cụ thể cần xoá (mỗi key phải bắt đầu bằng "pmis/").</summary>
        public List<string>? ObjectKeys { get; set; }

        /// <summary>Xoá mọi object có tiền tố này (phải bắt đầu bằng "pmis/"). Dùng thay ObjectKeys.</summary>
        public string? Prefix { get; set; }

        /// <summary>Trần số object xoá trong 1 lần (mặc định 1.000, tối đa 10.000); vượt trần thì báo và không xoá.</summary>
        public int? MaxDelete { get; set; }

        /// <summary>true = chỉ liệt kê những gì SẼ bị xoá, không xoá gì.</summary>
        public bool DryRun { get; set; }

        /// <summary>Đặt lại dòng PMIS_DOCUMENT tương ứng về PENDING để tải lại (mặc định true).</summary>
        public bool ResetDb { get; set; } = true;

        /// <summary>Phải đúng chữ "DELETE" thì mới xoá thật.</summary>
        public string? Confirm { get; set; }
    }

    /// <summary>Danh sách bucket.</summary>
    [HttpGet("buckets")]
    public async Task<IActionResult> Buckets([FromHeader(Name = "X-Debug-Sql-Secret")] string? secret)
    {
        if (!ValidateAccess(secret, requireAllowExecute: false, out var error)) return error!;
        var result = await _minio.ListBucketsAsync();
        return Ok(new
        {
            documentBucket = _fileStorage.DocumentBucketName,
            dossierBucket = _fileStorage.DossierBucketName,
            buckets = result.Buckets.Select(b => new { b.Name, b.CreationDateDateTime }),
        });
    }

    /// <summary>Liệt kê object. <paramref name="summary"/>=true chỉ đếm số object + tổng dung lượng (quét tối đa 500.000).</summary>
    [HttpGet("objects")]
    public async Task<IActionResult> Objects(
        [FromHeader(Name = "X-Debug-Sql-Secret")] string? secret,
        [FromQuery] string? bucket,
        [FromQuery] string? prefix,
        [FromQuery] int maxKeys = DefaultMaxKeys,
        [FromQuery] bool summary = false)
    {
        if (!ValidateAccess(secret, requireAllowExecute: false, out var error)) return error!;

        var bucketName = string.IsNullOrWhiteSpace(bucket) ? _fileStorage.DocumentBucketName : bucket;
        maxKeys = Math.Clamp(maxKeys, 1, HardMaxKeys);

        try
        {
            var args = new ListObjectsArgs().WithBucket(bucketName).WithPrefix(prefix ?? string.Empty).WithRecursive(true);
            var items = new List<object>();
            long count = 0, totalBytes = 0;
            var truncated = false;

            await foreach (var item in _minio.ListObjectsEnumAsync(args, HttpContext.RequestAborted))
            {
                if (item.IsDir) continue;
                count++;
                totalBytes += (long)item.Size;

                if (summary)
                {
                    if (count >= SummaryScanLimit) { truncated = true; break; }
                    continue;
                }

                if (items.Count >= maxKeys) { truncated = true; break; }
                items.Add(new { key = item.Key, size = item.Size, lastModified = item.LastModifiedDateTime, etag = item.ETag });
            }

            return summary
                ? Ok(new { bucket = bucketName, prefix, objectCount = count, totalBytes, totalMb = Math.Round(totalBytes / 1048576.0, 1), scanTruncated = truncated })
                : Ok(new { bucket = bucketName, prefix, returned = items.Count, truncated, objects = items });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DebugMinioController: lỗi liệt kê bucket {Bucket} prefix {Prefix}", bucketName, prefix);
            return StatusCode(500, new { message = "Lỗi khi liệt kê MinIO.", detail = ex.Message });
        }
    }

    /// <summary>Xoá object (chỉ bucket tài liệu, chỉ key bắt đầu bằng "pmis/"). Cần AllowExecute + confirm="DELETE" (hoặc dryRun=true).</summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromHeader(Name = "X-Debug-Sql-Secret")] string? secret,
        [FromBody] DeleteRequest request)
    {
        if (!ValidateAccess(secret, requireAllowExecute: true, out var error)) return error!;

        var hasKeys = request.ObjectKeys is { Count: > 0 };
        var hasPrefix = !string.IsNullOrWhiteSpace(request.Prefix);
        if (hasKeys == hasPrefix)
            return BadRequest(new { message = "Truyền đúng một trong hai: objectKeys hoặc prefix." });

        var maxDelete = Math.Clamp(request.MaxDelete ?? 1000, 1, HardMaxDelete);
        var bucketName = _fileStorage.DocumentBucketName;

        try
        {
            List<string> keys;
            if (hasKeys)
            {
                keys = request.ObjectKeys!.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
                var bad = keys.FirstOrDefault(k => !IsSafeKey(k));
                if (bad != null) return BadRequest(new { message = $"Key không hợp lệ (phải bắt đầu bằng '{DeletablePrefix}', không chứa '..'): {bad}" });
            }
            else
            {
                if (!IsSafeKey(request.Prefix!))
                    return BadRequest(new { message = $"prefix phải bắt đầu bằng '{DeletablePrefix}' và không chứa '..'." });

                keys = [];
                var args = new ListObjectsArgs().WithBucket(bucketName).WithPrefix(request.Prefix!).WithRecursive(true);
                await foreach (var item in _minio.ListObjectsEnumAsync(args, HttpContext.RequestAborted))
                {
                    if (item.IsDir) continue;
                    keys.Add(item.Key);
                    if (keys.Count > maxDelete) break;
                }
            }

            if (keys.Count > maxDelete)
                return BadRequest(new { message = $"Số object cần xoá vượt trần maxDelete={maxDelete}. Thu hẹp prefix hoặc nâng maxDelete (tối đa {HardMaxDelete}).", wouldDeleteAtLeast = keys.Count });

            if (request.DryRun)
                return Ok(new { dryRun = true, bucket = bucketName, wouldDelete = keys.Count, sample = keys.Take(20) });

            if (!string.Equals(request.Confirm, "DELETE", StringComparison.Ordinal))
                return BadRequest(new { message = "Thiếu xác nhận: đặt \"confirm\": \"DELETE\" để xoá thật (hoặc \"dryRun\": true để xem trước)." });

            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogWarning("DebugMinioController: XOÁ {Count} object trong bucket {Bucket} từ {RemoteIp}, resetDb={ResetDb}, ví dụ={Sample}",
                keys.Count, bucketName, remoteIp, request.ResetDb, string.Join(" | ", keys.Take(5)));

            var deleted = new List<string>();
            var failed = new List<object>();
            var sync = new object();
            await Parallel.ForEachAsync(keys, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = HttpContext.RequestAborted },
                async (key, ct) =>
                {
                    try
                    {
                        await _minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucketName).WithObject(key), ct);
                        lock (sync) deleted.Add(key);
                    }
                    catch (Exception ex)
                    {
                        lock (sync) failed.Add(new { key, error = ex.Message });
                    }
                });

            var dbRowsReset = 0;
            if (request.ResetDb && deleted.Count > 0)
            {
                _connection.EnsureOpen();
                foreach (var chunk in deleted.Chunk(500))
                {
                    dbRowsReset += await _connection.ExecuteAsync(@"
                        UPDATE PMIS_DOCUMENT
                        SET ObjectKey = NULL, FileSize = NULL, CONTENT_SHA256 = NULL, FILE_STATUS = 'PENDING', FILE_ATTEMPTS = 0,
                            FILE_NEXT_RETRY_AT = NULL, FILE_LAST_ERROR = NULL
                        WHERE ObjectKey IN :Keys", new { Keys = chunk.ToList() });
                }
            }

            return Ok(new
            {
                bucket = bucketName,
                requested = keys.Count,
                deleted = deleted.Count,
                failed = failed.Count,
                failures = failed.Take(20),
                dbRowsReset,
                note = "Bucket bật versioning thì chỉ tạo delete marker; phiên bản cũ vẫn còn.",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DebugMinioController: lỗi khi xoá");
            return StatusCode(500, new { message = "Lỗi khi xoá object MinIO.", detail = ex.Message });
        }
    }

    private static bool IsSafeKey(string key) =>
        key.StartsWith(DeletablePrefix, StringComparison.Ordinal) && !key.Contains("..") && !key.Contains('\\');

    private bool ValidateAccess(string? provided, bool requireAllowExecute, out IActionResult? errorResult)
    {
        var expected = _configuration["DebugSql:SecretKey"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "DebugSql:SecretKey chưa được cấu hình trên EquipmentService." });
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
            errorResult = StatusCode(503, new { message = "Xoá đang TẮT. Đặt DebugSql__AllowExecute=true trên EquipmentService (restart) để bật tạm, xong nhớ tắt lại." });
            return false;
        }

        errorResult = null;
        return true;
    }
}
