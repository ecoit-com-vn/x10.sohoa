namespace EvnHanoi.EquipmentService.Core.Services;

public interface IDocumentFulltextSearchNotificationClient
{
    Task<HttpResponseMessage> SearchDocumentsAsync(string? queryString, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> GetDocumentDetailAsync(
        string versionId,
        CancellationToken cancellationToken = default);
}

public class DocumentFulltextSearchNotificationClient : IDocumentFulltextSearchNotificationClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DocumentFulltextSearchNotificationClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<HttpResponseMessage> SearchDocumentsAsync(
        string? queryString,
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(queryString)
            ? "api/v1/search/documents"
            : $"api/v1/search/documents?{queryString}";
        return SendGetAsync(path, cancellationToken);
    }

    public Task<HttpResponseMessage> GetDocumentDetailAsync(
        string versionId,
        CancellationToken cancellationToken = default) =>
        SendGetAsync($"api/v1/search/documents/{Uri.EscapeDataString(versionId)}", cancellationToken);

    private async Task<HttpResponseMessage> SendGetAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("NotificationService");
        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
