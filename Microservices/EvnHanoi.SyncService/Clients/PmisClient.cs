using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Polly;
using Polly.Extensions.Http;

namespace EvnHanoi.SyncService.Clients;

public class PmisClient : IPmisClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // 1 circuit breaker RIÊNG cho mỗi cặp (HttpClient name, apiCode) — vd. "PMIS:SUBSTATION_LIST" và
    // "PMIS:DEVICE_QR_IMAGE" độc lập nhau, "PMIS:SUBSTATION_LIST" và "PMIS-Interactive:SUBSTATION_LIST"
    // cũng độc lập nhau. Trước đây circuit breaker gắn ở mức HttpClientFactory (1 policy dùng chung cho
    // cả named client "PMIS"), nên 1 API code lỗi liên tục (vd. DEVICE_QR_IMAGE do URL cấu hình sai) sẽ
    // "mở mạch" luôn cho các API code khác vẫn gọi PMIS bình thường (vd. SUBSTATION_LIST) — đã gặp thực
    // tế trên production. Dùng ConcurrentDictionary tĩnh (không phải field instance) vì PmisClient được
    // đăng ký Scoped — mỗi request/lượt job tạo instance mới, nếu lưu policy trong field instance thì
    // trạng thái "mở mạch" sẽ mất ngay khi scope kết thúc, vô hiệu hoá luôn tác dụng của circuit breaker.
    private static readonly ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>> CircuitBreakers = new();

    // internal (không private): PmisSyncWorker/PmisPublisherWorker gọi thẳng CreateClient("PMIS") mà
    // không qua PmisClient, nhưng vẫn cần circuit breaker riêng theo đúng nguyên tắc trên — dùng chung
    // registry này với key riêng (vd. "PMIS:LegacyPull", "PMIS:LegacyPush") thay vì bị chặn quyền truy cập.
    internal static IAsyncPolicy<HttpResponseMessage> GetCircuitBreaker(string key) =>
        CircuitBreakers.GetOrAdd(key, _ => HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

    // PmisClient (Scoped) dùng chung 1 IDbConnection với endpoint provider + repo log; ODP.NET connection KHÔNG thread-safe, nên khi
    // job tải file gọi song song nhiều DownloadDocumentFileByCodeAsync trên cùng instance, mọi truy cập DB phải tuần tự.
    private readonly SemaphoreSlim _dbGate = new(1, 1);

    private readonly IPmisEndpointConfigProvider _endpointConfigProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPmisApiCallLogRepository _apiCallLogRepository;
    private readonly string _httpClientName;

    /// <param name="httpClientName">Tên HttpClient đã đăng ký ở Program.cs — mặc định "PMIS" (dùng bởi
    /// đồng bộ nền: PmisSyncExecutionService/PmisScheduledSyncJob). <see cref="InteractivePmisClient"/>
    /// truyền "PMIS-Interactive" để có circuit breaker RIÊNG cho các API tra cứu/tìm kiếm tương tác
    /// (PmisLookupController, PmisManualSyncController.Search) — tránh việc đồng bộ nền gọi PMIS lỗi
    /// dồn dập làm mở circuit breaker chung, khoá luôn thao tác tra cứu/tìm kiếm của người dùng đang
    /// chờ trên màn hình trong lúc đó.</param>
    public PmisClient(
        IPmisEndpointConfigProvider endpointConfigProvider, IHttpClientFactory httpClientFactory,
        IPmisApiCallLogRepository apiCallLogRepository, string httpClientName = "PMIS")
    {
        _endpointConfigProvider = endpointConfigProvider;
        _httpClientFactory = httpClientFactory;
        _apiCallLogRepository = apiCallLogRepository;
        _httpClientName = httpClientName;
    }

    public Task<PmisListResponse<PmisSubstationDto>> GetSubstationsAsync(PmisSubstationSearchRequest request) =>
        GetListAsync<PmisSubstationDto>("SUBSTATION_LIST", request);

    public Task<PmisListResponse<PmisLineDto>> GetLinesAsync(PmisLineSearchRequest request) =>
        GetListAsync<PmisLineDto>("LINE_LIST", request);

    public Task<PmisListResponse<PmisDeviceTypeDto>> GetSubstationDeviceTypesAsync(PmisDeviceTypeSearchRequest request) =>
        GetListAsync<PmisDeviceTypeDto>("SUBSTATION_DEVICE_TYPE_LIST", request);

    public Task<PmisListResponse<PmisSubstationDeviceDto>> GetSubstationDevicesAsync(PmisSubstationDeviceSearchRequest request) =>
        GetListAsync<PmisSubstationDeviceDto>("SUBSTATION_DEVICE_LIST", request);

    public Task<PmisListResponse<PmisDeviceTypeDto>> GetLineDeviceTypesAsync(PmisDeviceTypeSearchRequest request) =>
        GetListAsync<PmisDeviceTypeDto>("LINE_DEVICE_TYPE_LIST", request);

    public Task<PmisListResponse<PmisLineDeviceDto>> GetLineDevicesAsync(PmisLineDeviceSearchRequest request) =>
        GetListAsync<PmisLineDeviceDto>("LINE_DEVICE_LIST", request);

    public async Task<PmisDeviceDetailDto?> GetDeviceDetailAsync(PmisDeviceDetailRequest request)
    {
        var response = await SendAsync("DEVICE_DETAIL", request);
        return await response.Content.ReadFromJsonAsync<PmisDeviceDetailDto>(JsonOptions);
    }

    public async Task<byte[]?> GetDeviceQrImageBytesAsync(string idPmis)
    {
        try
        {
            var response = await SendAsync("DEVICE_QR_IMAGE", new PmisDeviceQrImageRequest { IdPmis = idPmis });
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (PmisEndpointNotConfiguredException)
        {
            // API ảnh QR chưa được admin cấu hình URL — bỏ qua QR, không chặn phần còn lại của đồng bộ.
            return null;
        }
    }

    public Task<PmisListResponse<PmisSubstationDocumentDto>> GetSubstationDocumentsAsync(PmisSubstationDocumentSearchRequest request) =>
        GetListAsync<PmisSubstationDocumentDto>("SUBSTATION_DOCUMENT_LIST", request);

    public Task<PmisListResponse<PmisLineDocumentDto>> GetLineDocumentsAsync(PmisLineDocumentSearchRequest request) =>
        GetListAsync<PmisLineDocumentDto>("LINE_DOCUMENT_LIST", request);

    public async Task<bool> IsDocumentFileEndpointActiveAsync() =>
        await _endpointConfigProvider.GetEndpointAsync(PmisApiCodes.DocumentFileDownload) != null;

    /// <summary>
    /// Tải file tài liệu theo mã qua API cấu hình DOCUMENT_FILE_DOWNLOAD (xem <see cref="IPmisClient.DownloadDocumentFileByCodeAsync"/>).
    /// PMIS không còn trả URL file trong API danh sách (trường "file" bỏ không dùng) — mọi file lấy bằng 1 API cố định
    /// TaiFileTaiLieu?maTaiLieu=..., nên URL/phương thức/timeout/header là cấu hình admin, KHÔNG suy từ dữ liệu từng tài liệu.
    /// </summary>
    public async Task<DocumentFileDownloadResult> DownloadDocumentFileByCodeAsync(
        string maTaiLieu, long maxBytes, int spoolThresholdBytes, CancellationToken ct = default)
    {
        const string apiCode = PmisApiCodes.DocumentFileDownload;
        ResolvedPmisEndpoint? endpoint;
        await _dbGate.WaitAsync(ct);
        try { endpoint = await _endpointConfigProvider.GetEndpointAsync(apiCode); }
        finally { _dbGate.Release(); }
        if (endpoint == null)
            return DocumentFileDownloadResult.Fail(DocumentFileOutcomeKind.NotConfigured, "API DOCUMENT_FILE_DOWNLOAD chưa cấu hình hoặc đang tắt (màn Cấu hình kết nối API).");

        var query = $"maTaiLieu={Uri.EscapeDataString(maTaiLieu)}";
        var uri = endpoint.Url.Contains('?') ? $"{endpoint.Url}&{query}" : $"{endpoint.Url}?{query}";

        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? callError = null;
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(endpoint.HttpMethod), uri);
            foreach (var header in endpoint.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            var httpClient = _httpClientFactory.CreateClient(_httpClientName);
            if (endpoint.TimeoutSeconds is > 0) httpClient.Timeout = TimeSpan.FromSeconds(endpoint.TimeoutSeconds.Value);

            // HttpClient.Timeout và Polly timeout chỉ phủ tới khi nhận HEADER (ResponseHeadersRead) — thời gian đọc body tự giới hạn
            // bằng CancellationToken riêng (gấp 3 lần timeout cấu hình, tối thiểu 5 phút) để file lớn tải chậm không treo vô hạn.
            using var bodyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bodyCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(300, (endpoint.TimeoutSeconds ?? 100) * 3)));

            // Circuit breaker RIÊNG theo mã API này (không dùng chung với API danh sách) — tải file lỗi hàng loạt
            // không được mở mạch của API danh sách tài liệu vẫn gọi PMIS bình thường.
            var circuitBreaker = GetCircuitBreaker($"{_httpClientName}:{apiCode}");
            response = await circuitBreaker.ExecuteAsync(
                () => httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, bodyCts.Token));

            var status = (int)response.StatusCode;
            var kind = DocumentFileErrorClassifier.ClassifyStatus(status);
            if (kind != DocumentFileOutcomeKind.Ok)
            {
                var snippet = await ReadSnippetAsync(response.Content);
                var reason = $"PMIS trả về HTTP {status} {response.ReasonPhrase} ({uri}){(snippet.Length > 0 ? ": " + snippet : string.Empty)}";
                callError = new HttpRequestException(reason);
                return DocumentFileDownloadResult.Fail(kind, reason, status);
            }

            // 200 nhưng là JSON (thông báo lỗi nghiệp vụ của PMIS/gateway) thì KHÔNG phải file — không được lưu nhầm vào MinIO.
            if (DocumentFileErrorClassifier.IsJsonMediaType(response.Content.Headers.ContentType?.MediaType))
            {
                var snippet = await ReadSnippetAsync(response.Content);
                var reason = $"PMIS trả JSON thay vì file ({uri}): {snippet}";
                callError = new HttpRequestException(reason);
                return DocumentFileDownloadResult.Fail(DocumentFileOutcomeKind.Permanent, reason, status);
            }

            var declared = response.Content.Headers.ContentLength;
            if (declared > maxBytes)
            {
                var reason = $"File {declared / 1024 / 1024} MB vượt giới hạn {maxBytes / 1024 / 1024} MB (TOO_LARGE).";
                callError = new HttpRequestException(reason);
                return DocumentFileDownloadResult.Fail(DocumentFileOutcomeKind.Permanent, reason, status);
            }

            await using var body = await response.Content.ReadAsStreamAsync(bodyCts.Token);
            var spooled = await DocumentFileDownloadResult.SpoolAsync(body, declared, spoolThresholdBytes, maxBytes, status, bodyCts.Token);
            if (spooled == null)
            {
                var reason = $"File vượt giới hạn {maxBytes / 1024 / 1024} MB (TOO_LARGE).";
                callError = new HttpRequestException(reason);
                return DocumentFileDownloadResult.Fail(DocumentFileOutcomeKind.Permanent, reason, status);
            }

            if (spooled.Length == 0)
            {
                await spooled.DisposeAsync();
                callError = new HttpRequestException("PMIS trả về file rỗng.");
                return DocumentFileDownloadResult.Fail(DocumentFileOutcomeKind.Permanent, "PMIS trả về file rỗng.", status);
            }

            return spooled;
        }
        catch (OperationCanceledException ocex) when (ct.IsCancellationRequested)
        {
            callError = ocex; // bị huỷ chủ động (job dừng) — ghi log gọi API là chưa hoàn tất, không phải thành công
            throw;
        }
        catch (Exception ex)
        {
            callError = ex;
            var kind = DocumentFileErrorClassifier.ClassifyException(ex);
            if (kind != DocumentFileOutcomeKind.CircuitOpen)
                Serilog.Log.Warning(ex, "PmisClient: lỗi tải file tài liệu {MaTaiLieu} qua {Uri}.", maTaiLieu, uri);
            // Format (đầy đủ: lớp vỏ Polly + nguyên nhân gốc) vì đích là cột FILE_LAST_ERROR riêng của đúng tài liệu này.
            return DocumentFileDownloadResult.Fail(kind, SyncErrorFormatter.Format(ex));
        }
        finally
        {
            sw.Stop();
            await LogCallAsync(apiCode, endpoint.HttpMethod, uri, null, response, callError, sw.ElapsedMilliseconds, null);
            response?.Dispose();
        }
    }

    public async Task<int?> GetDocumentTotalAsync(bool isSubstation, string ownerPmisCode, DateTime? tuNgay = null, DateTime? denNgay = null)
    {
        // skip vượt xa mọi tổng thực tế → PMIS trả {total, items: []} (vài chục byte) — KHÔNG dùng take=0 (trả TOÀN BỘ tài liệu).
        const int skipBeyondAnyTotal = 10_000_000;
        try
        {
            if (isSubstation)
            {
                var r = await GetSubstationDocumentsAsync(new PmisSubstationDocumentSearchRequest
                    { MaTBA = ownerPmisCode, TuNgay = tuNgay, DenNgay = denNgay, Skip = skipBeyondAnyTotal, Take = 1 });
                return r.Total;
            }

            var l = await GetLineDocumentsAsync(new PmisLineDocumentSearchRequest
                { MaDuongDay = ownerPmisCode, TuNgay = tuNgay, DenNgay = denNgay, Skip = skipBeyondAnyTotal, Take = 1 });
            return l.Total;
        }
        catch (PmisEndpointNotConfiguredException)
        {
            return null;
        }
    }

    private static async Task<string> ReadSnippetAsync(HttpContent content)
    {
        try
        {
            var bytes = await ReadCappedAsync(content, 400);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            text = text.Length > 200 ? text[..200] : text;
            return text.All(c => !char.IsControl(c) || c is '\r' or '\n' or '\t') ? text.Replace("\r", " ").Replace("\n", " ").Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<PmisListResponse<T>> GetListAsync<T>(string apiCode, object request)
    {
        // suppressSuccessLog=true: API danh sách cần ghi log KÈM số bản ghi (RecordCount), chỉ biết được
        // sau khi đọc/parse xong body ở đây — nên tự ghi log thành công riêng (bên dưới) thay vì để
        // SendCoreAsync ghi ngay lúc response vừa về (khi đó chưa biết được bao nhiêu bản ghi). Lỗi thì
        // KHÔNG suppress được (không có cơ hội parse) — SendCoreAsync vẫn tự ghi log lỗi như cũ.
        var (response, httpMethod, uri, query, sw) = await SendCoreAsync(apiCode, request, suppressSuccessLog: true);

        PmisListResponse<T> parsed;
        try
        {
            parsed = await response.Content.ReadFromJsonAsync<PmisListResponse<T>>(JsonOptions) ?? new PmisListResponse<T>();
        }
        catch (Exception ex)
        {
            // HTTP đã thành công (SendCoreAsync không log vì suppressSuccessLog=true, chờ đọc xong body
            // mới log) nhưng đọc/parse JSON lỗi — nếu không tự ghi log ở đây, request này sẽ KHÔNG có
            // dòng nào trong PMIS_API_CALL_LOG dù đã thật sự gọi tới PMIS, làm mất dấu vết trong "Lịch
            // sử gọi API".
            sw.Stop();
            await LogCallAsync(apiCode, httpMethod, uri, query, response, ex, sw.ElapsedMilliseconds, null);
            throw;
        }

        sw.Stop();
        await LogCallAsync(apiCode, httpMethod, uri, query, response, null, sw.ElapsedMilliseconds, parsed.Items?.Count);

        return parsed;
    }

    public async Task<string> PeekDocumentsRawAsync(bool isSubstation, string ownerPmisCode, int take)
    {
        object request = isSubstation
            ? new PmisSubstationDocumentSearchRequest { MaTBA = ownerPmisCode, Skip = 0, Take = take }
            : new PmisLineDocumentSearchRequest { MaDuongDay = ownerPmisCode, Skip = 0, Take = take };
        using var response = await SendAsync(isSubstation ? "SUBSTATION_DOCUMENT_LIST" : "LINE_DOCUMENT_LIST", request);
        var bytes = await ReadCappedAsync(response.Content, ProbeMaxBodyBytes);
        if (bytes.Length >= ProbeMaxBodyBytes)
            throw new InvalidOperationException($"Phản hồi PMIS lớn hơn {ProbeMaxBodyBytes / 1024 / 1024} MB (có thể chứa file/base64 trong danh sách) — dùng take nhỏ hơn hoặc peek-api để xem phần đầu.");
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private const int ProbeMaxBodyBytes = 2 * 1024 * 1024;

    // Không cho caller ghi đè Host (đổi vhost đích của gateway) hay header điều khiển kết nối/độ dài.
    private static readonly HashSet<string> ProbeBlockedHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Host", "Content-Length", "Transfer-Encoding", "Connection", "Upgrade" };

    /// <summary>Đọc tối đa <paramref name="max"/> byte đầu của body (không nạp cả body lớn vào bộ nhớ).</summary>
    private static async Task<byte[]> ReadCappedAsync(HttpContent content, int max)
    {
        await using var stream = await content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while (ms.Length < max && (read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, max - ms.Length)))) > 0)
            ms.Write(buffer, 0, read);
        return ms.ToArray();
    }

    public Task<DocumentFileProbe> ProbeDocumentFileAsync(string maTaiLieu, string endpointApiCode,
        string method = "GET", string bodyMode = "query", string path = "/api/PmisDongBo/TaiFileTaiLieu")
    {
        var isPost = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);
        var request = new PmisProbeRequest { Method = method, Path = path, HeadersFromApiCode = endpointApiCode };
        if (!isPost || string.Equals(bodyMode, "query", StringComparison.OrdinalIgnoreCase))
            request.Query = new Dictionary<string, string> { ["maTaiLieu"] = maTaiLieu };
        else if (string.Equals(bodyMode, "json", StringComparison.OrdinalIgnoreCase))
            request.JsonBody = System.Text.Json.JsonSerializer.SerializeToElement(new { maTaiLieu });
        else if (string.Equals(bodyMode, "form", StringComparison.OrdinalIgnoreCase))
            request.FormBody = new Dictionary<string, string> { ["maTaiLieu"] = maTaiLieu };
        return ProbeApiAsync(request);
    }

    public async Task<DocumentFileProbe> ProbeApiAsync(PmisProbeRequest req)
    {
        var probe = new DocumentFileProbe { Method = req.Method.ToUpperInvariant() };
        try
        {
            var httpClient = _httpClientFactory.CreateClient(_httpClientName);
            var endpoint = await _endpointConfigProvider.GetEndpointAsync(req.HeadersFromApiCode);

            var queryString = req.Query is { Count: > 0 }
                ? string.Join("&", req.Query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"))
                : null;

            // Host tin cậy = gateway PMIS cấu hình + host của endpoint nguồn header; chỉ host này mới nhận header xác thực cấu hình.
            var trusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (httpClient.BaseAddress != null) trusted.Add(httpClient.BaseAddress.Authority);
            if (Uri.TryCreate(endpoint?.Url, UriKind.Absolute, out var endpointUri)) trusted.Add(endpointUri.Authority);
            var attachConfigHeaders = true;

            if (!string.IsNullOrWhiteSpace(req.Url))
            {
                if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var target)
                    || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps)
                    || !string.IsNullOrEmpty(target.UserInfo))
                    throw new InvalidOperationException("url phải là http/https tuyệt đối, không chứa user:pass@.");

                var allowed = new HashSet<string>(trusted, StringComparer.OrdinalIgnoreCase);
                foreach (var extra in req.ExtraAllowedAuthorities) allowed.Add(extra.Trim());
                if (!allowed.Contains(target.Authority) && !allowed.Contains(target.Host))
                    throw new InvalidOperationException(
                        $"Host '{target.Authority}' không nằm trong danh sách cho phép. Cho phép: {string.Join(", ", allowed)}. Thêm host vào DebugSql__ProbeAllowedHosts nếu cần.");

                attachConfigHeaders = trusted.Contains(target.Authority);
                probe.RequestUrl = queryString == null ? target.AbsoluteUri
                    : target.AbsoluteUri + (target.Query.Length > 0 ? "&" : "?") + queryString;
            }
            else
            {
                var relative = queryString == null ? req.Path : $"{req.Path}?{queryString}";
                probe.RequestUrl = PmisFileUrlResolver.Resolve(relative, httpClient.BaseAddress, endpoint?.Url);
            }

            using var request = new HttpRequestMessage(new HttpMethod(probe.Method), probe.RequestUrl);
            if (req.JsonBody is { } json) request.Content = JsonContent.Create(json);
            else if (req.FormBody is { Count: > 0 }) request.Content = new FormUrlEncodedContent(req.FormBody);

            if (endpoint != null && attachConfigHeaders)
                foreach (var header in endpoint.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (req.Headers != null)
                foreach (var header in req.Headers.Where(h => !ProbeBlockedHeaders.Contains(h.Key)))
                {
                    request.Headers.Remove(header.Key);
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

            using var response = await httpClient.SendAsync(request);
            probe.StatusCode = (int)response.StatusCode;
            probe.ContentType = response.Content.Headers.ContentType?.ToString();
            probe.ContentLength = response.Content.Headers.ContentLength;
            probe.ResponseHeaders = response.Headers.Concat(response.Content.Headers)
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);

            // Chỉ đọc tối đa ProbeMaxBodyBytes: file/base64 lớn không được nạp hết vào RAM chỉ để chẩn đoán.
            // ContentLength (header) vẫn cho biết kích thước thật nếu server gửi.
            var bytes = await ReadCappedAsync(response.Content, ProbeMaxBodyBytes);
            probe.BodyBytes = bytes.Length;
            probe.BodyTruncated = bytes.Length >= ProbeMaxBodyBytes;
            probe.HeadHex = Convert.ToHexString(bytes.AsSpan(0, Math.Min(16, bytes.Length)));

            var headChars = Math.Clamp(req.HeadChars, 50, 4000);
            var text = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, headChars * 4));
            var isPrintable = text.All(c => !char.IsControl(c) || c is '\r' or '\n' or '\t');
            probe.HeadText = isPrintable ? (text.Length > headChars ? text[..headChars] : text) : null;

            // Nhận dạng định dạng: nhị phân theo magic bytes, JSON (kèm dò chuỗi dài giống base64), hay base64 thuần.
            if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) probe.DetectedFormat = "PDF (nhị phân)";
            else if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) probe.DetectedFormat = "JPEG (nhị phân)";
            else if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) probe.DetectedFormat = "PNG (nhị phân)";
            else if (bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B) probe.DetectedFormat = "ZIP/OOXML (nhị phân)";
            else if (bytes.Length > 0 && (bytes[0] == (byte)'{' || bytes[0] == (byte)'['))
            {
                probe.DetectedFormat = "JSON";
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(bytes);
                    var node = doc.RootElement;
                    // Mảng gốc → mô tả phần tử đầu; có "items" → mô tả phần tử đầu của items (cấu trúc phân trang PMIS).
                    if (node.ValueKind == System.Text.Json.JsonValueKind.Array && node.GetArrayLength() > 0) node = node[0];
                    else if (node.ValueKind == System.Text.Json.JsonValueKind.Object
                             && node.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array && items.GetArrayLength() > 0)
                        node = items[0];
                    if (node.ValueKind == System.Text.Json.JsonValueKind.Object)
                        probe.JsonKeys = node.EnumerateObject().Select(pr =>
                        {
                            var desc = $"{pr.Name}:{pr.Value.ValueKind}";
                            if (pr.Value.ValueKind != System.Text.Json.JsonValueKind.String) return desc;
                            var str = pr.Value.GetString() ?? string.Empty;
                            var looksBase64 = str.Length > 200 && System.Text.RegularExpressions.Regex.IsMatch(str[..200], "^[A-Za-z0-9+/=\\r\\n]+$");
                            return desc + $"(len={str.Length}{(looksBase64 ? ", base64?" : "")})";
                        }).ToList();
                }
                catch { /* không phải JSON hợp lệ — giữ HeadText */ }
            }
            else if (bytes.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(text, "^[A-Za-z0-9+/=\\r\\n]+$")) probe.DetectedFormat = "Có vẻ là base64 thuần";
            else probe.DetectedFormat = bytes.Length == 0 ? "Rỗng" : "Không xác định";
        }
        catch (Exception ex)
        {
            probe.Error = ex.Message;
        }

        return probe;
    }

    private async Task<HttpResponseMessage> SendAsync(string apiCode, object request) =>
        (await SendCoreAsync(apiCode, request, suppressSuccessLog: false)).Response;

    private async Task<(HttpResponseMessage Response, string HttpMethod, string Uri, string? Query, Stopwatch Stopwatch)> SendCoreAsync(
        string apiCode, object request, bool suppressSuccessLog)
    {
        var endpoint = await _endpointConfigProvider.GetEndpointAsync(apiCode)
            ?? throw new PmisEndpointNotConfiguredException(apiCode, apiCode);

        var query = BuildQueryString(request);
        var uri = string.IsNullOrEmpty(query) ? endpoint.Url : $"{endpoint.Url}?{query}";

        using var httpRequest = new HttpRequestMessage(new HttpMethod(endpoint.HttpMethod), uri);
        foreach (var header in endpoint.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var httpClient = _httpClientFactory.CreateClient(_httpClientName);
        if (endpoint.TimeoutSeconds is > 0)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(endpoint.TimeoutSeconds.Value);
        }

        var circuitBreaker = GetCircuitBreaker($"{_httpClientName}:{apiCode}");
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? callError = null;
        try
        {
            response = await circuitBreaker.ExecuteAsync(() => httpClient.SendAsync(httpRequest));
            response.EnsureSuccessStatusCode();
            // Không sw.Stop()/trả về ngay ở đây — caller (GetListAsync) còn cần đọc/parse body xong mới
            // dừng đồng hồ và tự ghi log, để DurationMs phản ánh đúng toàn bộ thời gian gọi (kể cả đọc body).
            return (response, endpoint.HttpMethod, uri, query, sw);
        }
        catch (Exception ex)
        {
            callError = ex;
            throw; // giữ nguyên loại exception — PmisUpstreamFailure.Matches ở PmisManualSyncController vẫn nhận diện đúng
        }
        finally
        {
            if (callError != null || !suppressSuccessLog)
            {
                sw.Stop();
                await LogCallAsync(apiCode, endpoint.HttpMethod, uri, query, response, callError, sw.ElapsedMilliseconds, null);
            }
        }
    }

    /// <summary>Ghi 1 dòng lịch sử gọi PMIS thật (PMIS_API_CALL_LOG) — không được để việc ghi log làm
    /// hỏng luồng đồng bộ/tra cứu chính, tự bắt lỗi riêng, chỉ log Serilog Warning nếu ghi thất bại.</summary>
    private async Task LogCallAsync(
        string apiCode, string httpMethod, string uri, string? payload,
        HttpResponseMessage? response, Exception? error, long durationMs, int? recordCount)
    {
        await _dbGate.WaitAsync();
        try
        {
            await _apiCallLogRepository.InsertAsync(new PmisApiCallLog
            {
                ApiCode = apiCode,
                HttpMethod = httpMethod,
                Url = uri,
                RequestPayload = payload,
                StatusCode = response == null ? null : (int)response.StatusCode,
                IsSuccess = error == null,
                ErrorMessage = error == null ? null : SyncErrorFormatter.Format(error),
                DurationMs = durationMs,
                HttpClientName = _httpClientName,
                RecordCount = recordCount
            });
        }
        catch (Exception logEx)
        {
            Serilog.Log.Warning(logEx, "PmisClient: lỗi khi ghi lịch sử gọi API {ApiCode}.", apiCode);
        }
        finally
        {
            _dbGate.Release();
        }
    }

    private static string BuildQueryString(object request)
    {
        var parts = new List<string>();
        foreach (var prop in request.GetType().GetProperties())
        {
            var value = prop.GetValue(request);
            if (value is null) continue;

            var key = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
            var stringValue = value switch
            {
                DateTime dt => dt.ToString("o"),
                // PMIS dùng querystring kiểu JS/JSON (vd. ?kemQRCode=false) — bool mặc định C# ToString()
                // ra "True"/"False" viết hoa, phải hạ thường để khớp đúng ví dụ thật của PMIS.
                bool b => b ? "true" : "false",
                _ => value.ToString() ?? string.Empty
            };
            parts.Add($"{key}={Uri.EscapeDataString(stringValue)}");
        }
        return string.Join("&", parts);
    }
}
