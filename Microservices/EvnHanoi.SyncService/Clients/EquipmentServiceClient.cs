using System.Net.Http.Json;
using EvnHanoi.SyncService.Models.Internal;

namespace EvnHanoi.SyncService.Clients;

public class EquipmentServiceClient : IEquipmentServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _uploadClient; // timeout dài cho file lớn (xem Program.cs)
    private readonly string? _internalToken;

    public EquipmentServiceClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("EquipmentServiceInternal");
        _uploadClient = httpClientFactory.CreateClient("EquipmentServiceInternalUpload");
        _internalToken = configuration["Internal:Token"];
    }

    public async Task<List<UpsertInfrastructureFromPmisResult>> UpsertInfrastructureAsync(List<UpsertInfrastructureFromPmisRequest> items)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/infrastructure/upsert-from-pmis")
        {
            Content = JsonContent.Create(items)
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UpsertInfrastructureFromPmisResult>>() ?? [];
    }

    public async Task<List<UpsertEquipmentFromPmisResult>> UpsertEquipmentAsync(List<UpsertEquipmentFromPmisRequest> items)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/equipment/upsert-from-pmis")
        {
            Content = JsonContent.Create(items)
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UpsertEquipmentFromPmisResult>>() ?? [];
    }

    public async Task<List<SyncedInfrastructurePmisCode>> GetSyncedInfrastructurePmisCodesAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/infrastructure/synced-pmis-codes");
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<List<SyncedInfrastructurePmisCode>>() ?? [];
    }

    public async Task<int> GetRecentlyTransferredCountAsync(int sinceHours)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/equipment/recently-transferred-count?sinceHours={sinceHours}");
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        var result = await response.Content.ReadFromJsonAsync<RecentlyTransferredCountResponse>();
        return result?.Count ?? 0;
    }

    private class RecentlyTransferredCountResponse
    {
        public int Count { get; set; }
    }

    public async Task<List<UpsertPmisDocumentResult>> UpsertDocumentsAsync(List<UpsertPmisDocumentRequest> items)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/documents/upsert-from-pmis")
        {
            Content = JsonContent.Create(items)
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UpsertPmisDocumentResult>>() ?? [];
    }

    public async Task<List<PendingPmisDocumentFile>> GetPendingDocumentFilesAsync(int take, IReadOnlyList<string>? excludePrefixes = null)
    {
        var url = $"internal/v1/documents/pending-files?take={take}";
        if (excludePrefixes is { Count: > 0 }) url += "&excludePrefixes=" + Uri.EscapeDataString(string.Join(',', excludePrefixes));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<List<PendingPmisDocumentFile>>() ?? [];
    }

    public async Task<bool> UploadDocumentFileAsync(string pmisDocumentCode, Stream content, long length, string sha256Hex, CancellationToken ct = default)
    {
        using var body = new StreamContent(content, 81920);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        body.Headers.ContentLength = length;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"internal/v1/documents/{Uri.EscapeDataString(pmisDocumentCode)}/file") { Content = body };
        request.Headers.Add("X-Internal-Token", _internalToken);
        request.Headers.Add("X-File-Sha256", sha256Hex);

        using var response = await _uploadClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        var result = await response.Content.ReadFromJsonAsync<DocumentFileStoredResponse>(cancellationToken: ct);
        return result?.Attached ?? false;
    }

    public async Task<bool> TryAttachExistingFileByHashAsync(string pmisDocumentCode, string sha256Hex, long length, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"internal/v1/documents/{Uri.EscapeDataString(pmisDocumentCode)}/file-by-hash")
        {
            Content = JsonContent.Create(new { sha256 = sha256Hex, size = length })
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        var result = await response.Content.ReadFromJsonAsync<DocumentFileStoredResponse>(cancellationToken: ct);
        return result?.Attached ?? false;
    }

    public async Task ReportDocumentFileFailureAsync(string pmisDocumentCode, bool transient, string? message, int transientRetryMinutes, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"internal/v1/documents/{Uri.EscapeDataString(pmisDocumentCode)}/file-failure")
        {
            Content = JsonContent.Create(new { kind = transient ? "TRANSIENT" : "PERMANENT", message, retryMinutes = transientRetryMinutes })
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessOrThrowWithBodyAsync(response);
    }

    private class DocumentFileStoredResponse
    {
        public bool Attached { get; set; }
    }

    public async Task<List<PmisDocumentFileStatusDto>> GetDocumentFileStatusAsync(IReadOnlyCollection<string> codes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/documents/file-status")
        {
            Content = JsonContent.Create(codes)
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<List<PmisDocumentFileStatusDto>>() ?? [];
    }

    public async Task<List<SyncedInfrastructurePmisCode>> GetPendingDocumentOwnersAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/documents/pending-owner-infrastructures");
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<List<SyncedInfrastructurePmisCode>>() ?? [];
    }

    public async Task<Dictionary<string, int>> GetDocumentCountsByInfrastructureAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/documents/counts-by-infrastructure");
        request.Headers.Add("X-Internal-Token", _internalToken);

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        var rows = await response.Content.ReadFromJsonAsync<List<DocumentCountRow>>() ?? [];
        return rows.GroupBy(r => r.PmisCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(r => r.DocumentCount), StringComparer.OrdinalIgnoreCase);
    }

    private class DocumentCountRow
    {
        public string PmisCode { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
    }

    public async Task<PendingDocumentFileSummary> GetPendingDocumentSummaryAsync(IReadOnlyList<string>? excludePrefixes = null)
    {
        var url = "internal/v1/documents/pending-summary";
        if (excludePrefixes is { Count: > 0 }) url += "?excludePrefixes=" + Uri.EscapeDataString(string.Join(',', excludePrefixes));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
        return await response.Content.ReadFromJsonAsync<PendingDocumentFileSummary>()
            ?? new PendingDocumentFileSummary();
    }

    public async Task AttachDocumentFileAsync(AttachPmisDocumentFileRequest attach)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/documents/attach-file")
        {
            Content = JsonContent.Create(attach)
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessOrThrowWithBodyAsync(response);
    }

    /// <summary>Thay cho response.EnsureSuccessStatusCode() trần — EquipmentService trả message JSON rõ
    /// ràng khi lỗi (vd "Internal:Token chưa được cấu hình trên EquipmentService.", "Token nội bộ không hợp
    /// lệ.") nhưng EnsureSuccessStatusCode() KHÔNG đọc body, chỉ ném "Response status code does not
    /// indicate success: 401" chung chung — khiến 1 lỗi CẤU HÌNH TOÀN CỤC (token sai/thiếu, ảnh hưởng CẢ
    /// LƯỢT đồng bộ) trông giống hệt lỗi dữ liệu của TỪNG bản ghi trên "Lịch sử đồng bộ", admin phải tự đoán
    /// thay vì thấy thẳng nguyên nhân.</summary>
    private static async Task EnsureSuccessOrThrowWithBodyAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"EquipmentService trả {(int)response.StatusCode} {response.StatusCode}: {(string.IsNullOrWhiteSpace(body) ? "(không có nội dung)" : body)}",
            inner: null, response.StatusCode);
    }
}
