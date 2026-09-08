using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Clients;

/// <inheritdoc cref="IInteractivePmisClient"/>
public class InteractivePmisClient : PmisClient, IInteractivePmisClient
{
    public InteractivePmisClient(
        IPmisEndpointConfigProvider endpointConfigProvider, IHttpClientFactory httpClientFactory,
        IPmisApiCallLogRepository apiCallLogRepository)
        : base(endpointConfigProvider, httpClientFactory, apiCallLogRepository, "PMIS-Interactive")
    {
    }
}
