using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EvnHanoi.SyncService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Workers;

public class PmisSyncWorker : BackgroundService
{
    private readonly ILogger<PmisSyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsyncRetryPolicy _httpRetryPolicy;
    private readonly IPmisSyncTriggerService _triggerService;

    // Lấy URL từ cấu hình - không hardcode
    private readonly string? _pmisApiUrl;
    private readonly bool _pmisEnabled;

    private readonly IConnection _connection;
    private IChannel? _channel;
    private readonly string _queueName = "equipment_sync_queue";

    public PmisSyncWorker(
        ILogger<PmisSyncWorker> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IPmisSyncTriggerService triggerService,
        IConnection connection)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _triggerService = triggerService;
        _connection = connection;

        // Lấy URL PMIS từ cấu hình, không cấu hình thì disable worker
        _pmisApiUrl = _configuration["Pmis:ApiUrl"];
        _pmisEnabled = !string.IsNullOrWhiteSpace(_pmisApiUrl);

        if (!_pmisEnabled)
        {
            _logger.LogWarning(
                "PmisSyncWorker: Cấu hình 'Pmis:ApiUrl' chưa được thiết lập. " +
                "Worker sẽ chạy ở chế độ chờ và bỏ qua đồng bộ PMIS. " +
                "Vui lòng cấu hình 'Pmis:ApiUrl' trong appsettings.json khi PMIS sẵn sàng.");
        }

        // Tích hợp Polly Retry Policy cho HTTP Client
        _httpRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "PMIS Sync HTTP call failed. Retry {RetryCount} after {Seconds}s. Error: {Error}",
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                });
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Chỉ khởi tạo RabbitMQ nếu PMIS được bật
        if (_pmisEnabled)
        {
            await TryInitializeRabbitMQAsync(cancellationToken);
        }
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Khởi tạo kênh RabbitMQ từ kết nối dùng chung.
    /// </summary>
    private async Task TryInitializeRabbitMQAsync(CancellationToken cancellationToken)
    {
        try
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "PmisSyncWorker: Khởi tạo kênh RabbitMQ thành công. Queue: {Queue}", _queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PmisSyncWorker: Không thể khởi tạo kênh RabbitMQ từ kết nối dùng chung.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Nếu chưa cấu hình PMIS URL, worker chạy nhưng không làm gì (chờ restart với config đúng)
        if (!_pmisEnabled)
        {
            _logger.LogInformation(
                "PmisSyncWorker: Chạy ở chế độ tạm dừng vì 'Pmis:ApiUrl' chưa được cấu hình.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var syncIntervalMinutes = _configuration.GetValue<int>("Pmis:SyncIntervalMinutes", 10);
        var delay = TimeSpan.FromMinutes(syncIntervalMinutes);

        _logger.LogInformation(
            "PmisSyncWorker: Bắt đầu chu trình đồng bộ PMIS. Mỗi {Minutes} phút một lần.", syncIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("PmisSyncWorker: Bắt đầu đồng bộ dữ liệu từ PMIS...");

            try
            {
                await SyncFromPmisAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Service đang dừng - thoát vòng lặp bình thường
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PmisSyncWorker: Lỗi trong quá trình đồng bộ PMIS.");
            }

            _logger.LogInformation(
                "PmisSyncWorker: Hoàn thành chu kỳ. Chờ {Minutes} phút trước chu kỳ tiếp theo.", delay.TotalMinutes);

            try
            {
                var triggerToken = _triggerService.GetTriggerToken();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, triggerToken);
                await Task.Delay(delay, linkedCts.Token);
            }
            catch (TaskCanceledException)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                _logger.LogInformation("PmisSyncWorker: Được kích hoạt chạy ngay lập tức.");
            }
        }

        _logger.LogInformation("PmisSyncWorker: Worker đã dừng.");
    }

    private async Task SyncFromPmisAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("PMIS");

        string responseString;
        try
        {
            responseString = await _httpRetryPolicy.ExecuteAsync(async () =>
            {
                var response = await client.GetAsync(_pmisApiUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            });
        }
        catch (HttpRequestException ex)
        {
            // PMIS không khả dụng - log warning và bỏ qua chu kỳ này
            _logger.LogWarning(
                "PmisSyncWorker: Không thể kết nối đến PMIS ({Url}). Bỏ qua chu kỳ đồng bộ này. Lỗi: {Error}",
                _pmisApiUrl, ex.Message);
            return;
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            // Circuit Breaker đang mở - đợi cho đến chu kỳ tiếp theo
            _logger.LogWarning(
                "PmisSyncWorker: Circuit Breaker đang MỞ (PMIS không khả dụng liên tục). " +
                "Bỏ qua chu kỳ đồng bộ này. Thời gian nghỉ: {Message}", ex.Message);
            return;
        }

        try
        {
            var equipments = JsonSerializer.Deserialize<EquipmentSyncMessage[]>(
                responseString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (equipments == null || equipments.Length == 0)
            {
                _logger.LogInformation("PmisSyncWorker: PMIS trả về danh sách thiết bị rỗng.");
                return;
            }

            // Thử kết nối lại RabbitMQ nếu chưa có hoặc đã mất
            if (_channel == null || !_channel.IsOpen)
            {
                _logger.LogWarning("PmisSyncWorker: Kênh RabbitMQ đóng. Thử khởi tạo lại...");
                await TryInitializeRabbitMQAsync(cancellationToken);
            }

            if (_channel == null || !_channel.IsOpen)
            {
                _logger.LogError(
                    "PmisSyncWorker: Không thể kết nối RabbitMQ. " +
                    "Bỏ qua {Count} bản ghi thiết bị từ PMIS.", equipments.Length);
                return;
            }

            foreach (var equipment in equipments)
            {
                var messageString = JsonSerializer.Serialize(equipment);
                var body = Encoding.UTF8.GetBytes(messageString);

                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _queueName,
                    mandatory: false,
                    basicProperties: new BasicProperties(),
                    body: body,
                    cancellationToken: cancellationToken);
            }

            _logger.LogInformation(
                "PmisSyncWorker: Đẩy thành công {Count} bản ghi thiết bị từ PMIS vào queue '{Queue}'.",
                equipments.Length, _queueName);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "PmisSyncWorker: Không thể parse phản hồi từ PMIS. " +
                "Response có thể rỗng hoặc sai định dạng.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel != null)
            {
                await _channel.CloseAsync(cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PmisSyncWorker: Lỗi khi đóng kênh RabbitMQ.");
        }
        await base.StopAsync(cancellationToken);
    }
}
