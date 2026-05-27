using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using RedLockNet;
using System.Net.Http;

namespace EvnHanoi.SyncService.Schedulers
{
    public class PmisSyncScheduler : IJob
    {
        private readonly IDistributedLockFactory _lockFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PmisSyncScheduler> _logger;

        public PmisSyncScheduler(
            IDistributedLockFactory lockFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<PmisSyncScheduler> logger)
        {
            _lockFactory = lockFactory;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var resource = "lock:pmissync";
            var expiry = TimeSpan.FromMinutes(2);
            var wait = TimeSpan.FromSeconds(10);
            var retry = TimeSpan.FromSeconds(1);

            _logger.LogInformation("Attempting to acquire distributed lock for PMIS sync...");

            using (var redLock = await _lockFactory.CreateLockAsync(resource, expiry, wait, retry))
            {
                if (redLock.IsAcquired)
                {
                    _logger.LogInformation("Lock acquired. Starting PMIS sync.");

                    try
                    {
                        var client = _httpClientFactory.CreateClient("PMIS");
                        var response = await client.GetAsync("/api/data"); // Mock API

                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            _logger.LogInformation("Successfully pulled data from PMIS: {DataLength} bytes.", content.Length);
                            // TODO: Process and save data to DB
                        }
                        else
                        {
                            _logger.LogWarning("Failed to pull data from PMIS. Status Code: {StatusCode}", response.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred during PMIS sync.");
                    }

                    _logger.LogInformation("PMIS sync completed. Releasing lock.");
                }
                else
                {
                    _logger.LogInformation("Another instance is already running PMIS sync. Skipping this trigger.");
                }
            }
        }
    }
}
