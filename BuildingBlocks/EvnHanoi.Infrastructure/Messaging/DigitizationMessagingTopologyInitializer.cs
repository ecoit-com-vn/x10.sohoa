using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EvnHanoi.Infrastructure.Messaging;

/// <summary>
/// Khai báo topology OCR ngay khi service start — không phụ thuộc thứ tự khởi động Aspire.
/// </summary>
public class DigitizationMessagingTopologyInitializer : IHostedService
{
    private readonly IConnection _connection;
    private readonly ILogger<DigitizationMessagingTopologyInitializer> _logger;

    public DigitizationMessagingTopologyInitializer(
        IConnection connection,
        ILogger<DigitizationMessagingTopologyInitializer> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await DigitizationTopicTopology.EnsureAsync(channel, cancellationToken);
        _logger.LogInformation("Digitization RabbitMQ topology ensured on {VirtualHost}.", _connection.Endpoint);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
