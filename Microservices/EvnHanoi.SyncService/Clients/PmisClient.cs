using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Clients;

public class PmisClient : IPmisClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

    /// <summary>
    /// Tải file nhị phân tài liệu từ URL PMIS trả về trong field "File" (API 8/9) — khác các API khác,
    /// URL này ĐỘNG theo từng tài liệu nên không resolve qua cấu hình endpoint như <see cref="SendAsync"/>.
    /// Vẫn đính kèm header đã cấu hình cho đúng endpoint nguồn (<paramref name="endpointApiCode"/> —
    /// SUBSTATION_DOCUMENT_LIST hoặc LINE_DOCUMENT_LIST, mỗi endpoint có thể cấu hình header/API key
    /// khác nhau) phòng trường hợp cần xác thực như AnhQRCode.
    /// Trả về null nếu tải lỗi — KHÔNG throw, để caller tự quyết định ghi cảnh báo mà không chặn đồng bộ.
    /// </summary>
    public async Task<byte[]?> DownloadDocumentFileAsync(string fileUrl, string endpointApiCode)
    {
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? callError = null;
        try
        {
            var endpoint = await _endpointConfigProvider.GetEndpointAsync(endpointApiCode);
            using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
            if (endpoint != null)
            {
                foreach (var header in endpoint.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var httpClient = _httpClientFactory.CreateClient(_httpClientName);
            response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            callError = ex;
            Serilog.Log.Warning(ex, "PmisClient: lỗi tải file tài liệu từ URL {FileUrl}.", fileUrl);
            return null;
        }
        finally
        {
            sw.Stop();
            await LogCallAsync(endpointApiCode, "GET", fileUrl, null, response, callError, sw.ElapsedMilliseconds, null);
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

        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? callError = null;
        try
        {
            response = await httpClient.SendAsync(httpRequest);
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
