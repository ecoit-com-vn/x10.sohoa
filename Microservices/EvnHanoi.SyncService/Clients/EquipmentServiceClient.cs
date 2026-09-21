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
        response.EnsureSuccessStatusCode();
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
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UpsertEquipmentFromPmisResult>>() ?? [];
    }

    public async Task<List<SyncedInfrastructurePmisCode>> GetSyncedInfrastructurePmisCodesAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/infrastructure/synced-pmis-codes");
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SyncedInfrastructurePmisCode>>() ?? [];
    }

    public async Task<List<LineNameIndexEntry>> GetLineNameIndexAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/infrastructure/line-name-index");
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<LineNameIndexEntry>>() ?? [];
    }

    public async Task<List<LineNameIndexEntry>> GetLinesNeedingBackfillAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/infrastructure/lines-needing-backfill");
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<LineNameIndexEntry>>() ?? [];
    }

    public async Task<int> BackfillLineParentsAsync(List<BackfillLineParentItem> items)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/infrastructure/backfill-line-parents")
        {
            Content = JsonContent.Create(new BackfillLineParentRequest { Items = items })
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BackfillLineParentResult>();
        return result?.UpdatedCount ?? 0;
    }

    public async Task<List<UpsertPmisDocumentResult>> UpsertDocumentsAsync(List<UpsertPmisDocumentRequest> items)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/documents/upsert-from-pmis")
        {
            Content = JsonContent.Create(items)
        };
        request.Headers.Add("X-Internal-Token", _internalToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UpsertPmisDocumentResult>>() ?? [];
    }
}
