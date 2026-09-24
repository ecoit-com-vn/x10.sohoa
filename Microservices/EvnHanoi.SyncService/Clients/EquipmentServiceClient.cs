using System.Net.Http.Json;
using EvnHanoi.SyncService.Models.Internal;

namespace EvnHanoi.SyncService.Clients;

public class EquipmentServiceClient : IEquipmentServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _internalToken;

    public EquipmentServiceClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("EquipmentServiceInternal");
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
