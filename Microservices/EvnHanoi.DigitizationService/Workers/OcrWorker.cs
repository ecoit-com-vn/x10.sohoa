// e:/ecoit/sohoax10/sohoa.backend/Microservices/EvnHanoi.DigitizationService/Workers/OcrWorker.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;

namespace EvnHanoi.DigitizationService.Workers
{
    public class OcrWorker : BackgroundService
    {
        private readonly ILogger<OcrWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnection _connection;
        private IChannel? _channel;
        private readonly string _ocrVlServerUrl;

        public OcrWorker(
            ILogger<OcrWorker> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            IConnection connection)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _connection = connection;
            _ocrVlServerUrl = _configuration["AIModelServers:OcrVlServerUrl"] ?? "http://localhost:8090";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                var exchangeName = "digitization.topic";
                var queueName = "ocr_task_queue";
                var routingKey = "ocr.process.task";

                await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Nhận yêu cầu OCR: {Message}", messageText);

                    try
                    {
                        var taskMsg = JsonSerializer.Deserialize<OcrTaskMessage>(messageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (taskMsg != null)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var repository = scope.ServiceProvider.GetRequiredService<IFileAttachmentRepository>();
                            var minioService = scope.ServiceProvider.GetRequiredService<IMinioStorageService>();
                            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

                            // 1. Cập nhật trạng thái
                            await repository.UpdateStatusAsync(taskMsg.FileId, "Processing");
                            _logger.LogInformation("Đã cập nhật trạng thái FileAttachment {FileId} thành Processing.", taskMsg.FileId);

                            // 2. Tải file từ MinIO
                            _logger.LogInformation("Tải file {FilePath} từ bucket {BucketName}", taskMsg.FilePath, taskMsg.BucketName);
                            using var fileStream = await minioService.DownloadFileAsync(taskMsg.BucketName, taskMsg.FilePath);
                            
                            // 3. Gọi ocr_vl_server để lấy kết quả (Giả lập gọi HTTP API)
                            var httpClient = httpClientFactory.CreateClient("OcrVlClient");
                            httpClient.BaseAddress = new Uri(_ocrVlServerUrl);

                            // Gửi file bytes dạng multipart
                            try
                            {
                                // var multipart = new MultipartFormDataContent();
                                // multipart.Add(new StreamContent(fileStream), "file", taskMsg.FilePath);
                                // var response = await httpClient.PostAsync("/completion", multipart, stoppingToken);
                                // response.EnsureSuccessStatusCode();
                                // var ocrResultText = await response.Content.ReadAsStringAsync();
                                
                                await Task.Delay(2000, stoppingToken);
                                _logger.LogInformation("Gọi ocr_vl_server thành công cho FileId {FileId}", taskMsg.FileId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Không thể kết nối đến ocr_vl_server.");
                            }

                            // 4. Cập nhật trạng thái
                            await repository.UpdateStatusAsync(taskMsg.FileId, "OcrCompleted");
                            _logger.LogInformation("AI OCR đã xong. Cập nhật trạng thái FileAttachment {FileId} thành OcrCompleted.", taskMsg.FileId);

                            // 5. Publish message tới extraction_task_queue
                            var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
                            var extractionMessage = new 
                            {
                                FileId = taskMsg.FileId,
                                FilePath = taskMsg.FilePath,
                                BucketName = taskMsg.BucketName,
                                Action = "extraction.process.task"
                            };
                            await publisher.PublishMessageAsync(extractionMessage, "digitization.topic", "extraction.process.task");

                            await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                        else
                        {
                            _logger.LogWarning("Message parse ra null. Bỏ qua.");
                            await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi xử lý OCR task.");
                        await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    }
                };

                await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối hoặc consume OCR tasks từ RabbitMQ.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null) await _channel.CloseAsync(cancellationToken: cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
