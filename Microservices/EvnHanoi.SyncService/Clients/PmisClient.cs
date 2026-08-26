using System.Net.Http.Json;
using System.Text.Json;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Clients;

public class PmisClient : IPmisClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IPmisEndpointConfigProvider _endpointConfigProvider;
    private readonly IHttpClientFactory _httpClientFactory;

    public PmisClient(IPmisEndpointConfigProvider endpointConfigProvider, IHttpClientFactory httpClientFactory)
    {
        _endpointConfigProvider = endpointConfigProvider;
        _httpClientFactory = httpClientFactory;
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

    private async Task<PmisListResponse<T>> GetListAsync<T>(string apiCode, object request)
    {
        var response = await SendAsync(apiCode, request);
        var parsed = await response.Content.ReadFromJsonAsync<PmisListResponse<T>>(JsonOptions);
        return parsed ?? new PmisListResponse<T>();
    }

    private async Task<HttpResponseMessage> SendAsync(string apiCode, object request)
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

        var httpClient = _httpClientFactory.CreateClient("PMIS");
        if (endpoint.TimeoutSeconds is > 0)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(endpoint.TimeoutSeconds.Value);
        }

        var response = await httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        return response;
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
