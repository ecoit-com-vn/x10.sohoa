using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Clients;

/// <inheritdoc cref="IInteractivePmisClient"/>
public class InteractivePmisClient : PmisClient, IInteractivePmisClient
{
    public InteractivePmisClient(IPmisEndpointConfigProvider endpointConfigProvider, IHttpClientFactory httpClientFactory)
        : base(endpointConfigProvider, httpClientFactory, "PMIS-Interactive")
    {
    }
}
