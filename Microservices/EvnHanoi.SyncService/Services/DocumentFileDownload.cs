using System.Security.Cryptography;

namespace EvnHanoi.SyncService.Services;

/// <summary>Cấu hình job tải file tài liệu PMIS — section "Pmis:DocumentFile" (biến môi trường Pmis__DocumentFile__*).</summary>
public class PmisDocumentFileOptions
{
    public const string SectionName = "Pmis:DocumentFile";

    /// <summary>Số tài liệu lấy mỗi lượt (mỗi phút 1 lượt).</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Số luồng tải song song tối đa; điều tốc tự hạ khi PMIS trả lỗi tạm thời và tăng lại khi ổn.</summary>
    public int MaxParallel { get; set; } = 4;

    /// <summary>Số luồng bắt đầu của mỗi lần khởi động pod (canary), tăng dần tới <see cref="MaxParallel"/>.</summary>
    public int InitialParallel { get; set; } = 2;

    /// <summary>Kích thước tối đa 1 file (byte); lớn hơn → lỗi vĩnh viễn TOO_LARGE, không lưu.</summary>
    public long MaxBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>File ≤ ngưỡng này giữ trong bộ nhớ; lớn hơn ghi ra file tạm (xoá ngay sau khi gửi xong).</summary>
    public int SpoolThresholdBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>Lỗi tạm thời (5xx/timeout/429): chờ bao nhiêu phút rồi thử lại (không tính vào số lần thử).</summary>
    public int TransientRetryMinutes { get; set; } = 10;

    /// <summary>Danh sách tiền tố mã tài liệu KHÔNG tải file (phân tách bằng dấu phẩy, vd "TA-") — để trống = tải tất cả.</summary>
    public string ExcludedCodePrefixes { get; set; } = string.Empty;

    public IReadOnlyList<string> ExcludedPrefixList =>
        ExcludedCodePrefixes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public enum DocumentFileOutcomeKind
{
    /// <summary>200 + nội dung nhị phân hợp lệ.</summary>
    Ok,

    /// <summary>Lỗi của riêng tài liệu (404/400, 200 nhưng là JSON, rỗng, quá lớn) — tính vào số lần thử.</summary>
    Permanent,

    /// <summary>PMIS quá tải/mạng (5xx, 429, timeout...) — KHÔNG tính vào số lần thử của tài liệu.</summary>
    Transient,

    /// <summary>401/403 — lỗi cấu hình toàn cục (header xác thực), dừng cả lượt.</summary>
    Unauthorized,

    /// <summary>Circuit breaker đang mở — chưa gọi PMIS thật, để nguyên tài liệu.</summary>
    CircuitOpen,

    /// <summary>API DOCUMENT_FILE_DOWNLOAD chưa cấu hình/đang tắt.</summary>
    NotConfigured
}

/// <summary>Phân loại lỗi tải file — logic thuần để kiểm thử.</summary>
public static class DocumentFileErrorClassifier
{
    public static DocumentFileOutcomeKind ClassifyStatus(int statusCode) => statusCode switch
    {
        401 or 403 => DocumentFileOutcomeKind.Unauthorized,
        408 or 425 or 429 => DocumentFileOutcomeKind.Transient,
        >= 500 => DocumentFileOutcomeKind.Transient,
        >= 400 => DocumentFileOutcomeKind.Permanent,
        _ => DocumentFileOutcomeKind.Ok
    };

    /// <summary>Ngoại lệ khi gọi/đọc: circuit mở → CircuitOpen; còn lại (timeout, mất kết nối, IO) → Transient.</summary>
    public static DocumentFileOutcomeKind ClassifyException(Exception ex) => ex switch
    {
        Polly.CircuitBreaker.BrokenCircuitException => DocumentFileOutcomeKind.CircuitOpen,
        _ => DocumentFileOutcomeKind.Transient
    };

    public static bool IsJsonMediaType(string? mediaType) =>
        string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Kết quả 1 lần tải file. Nội dung giữ trong bộ nhớ (nhỏ) hoặc file tạm (lớn) — gọi <see cref="DisposeAsync"/>
/// để xoá file tạm. Không bao giờ giữ cả file lớn trong bộ nhớ.</summary>
public sealed class DocumentFileDownloadResult : IAsyncDisposable
{
    private byte[]? _memory;
    private string? _tempPath;

    public DocumentFileOutcomeKind Kind { get; init; }
    public int? StatusCode { get; init; }
    public string? Reason { get; init; }
    public long Length { get; private set; }
    public string? Sha256Hex { get; private set; }

    public bool IsOk => Kind == DocumentFileOutcomeKind.Ok;

    public static DocumentFileDownloadResult Fail(DocumentFileOutcomeKind kind, string reason, int? statusCode = null) =>
        new() { Kind = kind, Reason = reason, StatusCode = statusCode };

    /// <summary>Sao chép <paramref name="source"/> vào bộ nhớ (≤ ngưỡng) hoặc file tạm, tính SHA-256 và độ dài; vượt
    /// <paramref name="maxBytes"/> thì dừng và trả null (caller báo TOO_LARGE). <paramref name="expectedLength"/> chỉ gợi ý chọn bộ nhớ/tạm.</summary>
    public static async Task<DocumentFileDownloadResult?> SpoolAsync(
        Stream source, long? expectedLength, int spoolThreshold, long maxBytes, int? statusCode, CancellationToken ct)
    {
        var result = new DocumentFileDownloadResult { Kind = DocumentFileOutcomeKind.Ok, StatusCode = statusCode };
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;

        var useTemp = expectedLength is > 0 && expectedLength.Value > spoolThreshold;
        Stream? sink = null;
        MemoryStream? memory = null;
        try
        {
            if (useTemp) sink = OpenTemp(result);
            else sink = memory = new MemoryStream();

            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                total += read;
                if (total > maxBytes) { await result.DisposeAsync(); await sink.DisposeAsync(); return null; }

                // Không biết trước độ dài (chunked) mà vượt ngưỡng bộ nhớ → chuyển sang file tạm.
                if (memory != null && total > spoolThreshold)
                {
                    var temp = OpenTemp(result);
                    memory.Position = 0;
                    await memory.CopyToAsync(temp, ct);
                    await memory.DisposeAsync();
                    memory = null;
                    sink = temp;
                }

                hash.AppendData(buffer, 0, read);
                await sink.WriteAsync(buffer.AsMemory(0, read), ct);
            }

            result.Length = total;
            result.Sha256Hex = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (memory != null) result._memory = memory.ToArray();
            else await sink.FlushAsync(ct);
            return result;
        }
        catch
        {
            await result.DisposeAsync();
            throw;
        }
        finally
        {
            if (sink != null) await sink.DisposeAsync();
        }
    }

    private static FileStream OpenTemp(DocumentFileDownloadResult result)
    {
        result._tempPath = Path.Combine(Path.GetTempPath(), $"pmis-doc-{Guid.NewGuid():N}.tmp");
        return new FileStream(result._tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
    }

    /// <summary>Mở luồng đọc nội dung (gọi được nhiều lần; caller đóng luồng).</summary>
    public Stream OpenRead() => _tempPath != null
        ? new FileStream(_tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan)
        : new MemoryStream(_memory ?? [], writable: false);

    public ValueTask DisposeAsync()
    {
        _memory = null;
        if (_tempPath != null)
        {
            try { File.Delete(_tempPath); } catch { /* file tạm — hệ điều hành dọn khi pod khởi động lại */ }
            _tempPath = null;
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>Điều tốc số luồng tải: hạ khi tỷ lệ lỗi tạm thời cao, tăng lại khi ổn, nghỉ khi lỗi dồn dập. Logic thuần, thread-safe,
/// dùng chung giữa các lượt job (singleton tĩnh vì Quartz tạo job mới mỗi lượt).</summary>
public sealed class DocumentFileThrottle
{
    public const int WindowSize = 20;
    public const double HighTransientRatio = 0.30;
    public const double LowTransientRatio = 0.05;
    public const int ConsecutiveTransientToAbort = 10;
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    private readonly object _gate = new();
    private readonly Queue<bool> _window = new(); // true = lỗi tạm thời
    private int _parallel;
    private int _consecutiveTransient;
    private int _goodWindows;
    private DateTime _cooldownUntilUtc = DateTime.MinValue;

    public DocumentFileThrottle(int initialParallel) => _parallel = Math.Max(1, initialParallel);

    public int Parallel { get { lock (_gate) return _parallel; } }
    public bool InCooldown(DateTime nowUtc) { lock (_gate) return nowUtc < _cooldownUntilUtc; }

    /// <summary>true nếu nên bỏ phần còn lại của lô (quá nhiều lỗi tạm thời liên tiếp).</summary>
    public bool ShouldAbortBatch { get { lock (_gate) return _consecutiveTransient >= ConsecutiveTransientToAbort; } }

    public void ResetBatch() { lock (_gate) _consecutiveTransient = 0; }

    /// <summary>Ghi 1 kết quả. <paramref name="transient"/>=true với lỗi tạm thời (5xx/timeout/429).</summary>
    public void Record(bool transient, int maxParallel, DateTime nowUtc)
    {
        lock (_gate)
        {
            _consecutiveTransient = transient ? _consecutiveTransient + 1 : 0;
            _window.Enqueue(transient);
            if (_window.Count < WindowSize) return;
            while (_window.Count > WindowSize) _window.Dequeue();

            var ratio = _window.Count(x => x) / (double)WindowSize;
            if (ratio > HighTransientRatio)
            {
                _parallel = Math.Max(1, _parallel / 2);
                _cooldownUntilUtc = nowUtc + Cooldown;
                _goodWindows = 0;
                _window.Clear();
            }
            else if (ratio < LowTransientRatio)
            {
                if (++_goodWindows >= 2)
                {
                    _parallel = Math.Min(Math.Max(1, maxParallel), _parallel + 1);
                    _goodWindows = 0;
                }
                _window.Clear();
            }
            else
            {
                _goodWindows = 0;
                _window.Clear();
            }
        }
    }
}
