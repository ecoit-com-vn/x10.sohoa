// e:/ecoit/sohoax10/sohoa.backend/Microservices/EvnHanoi.DigitizationService/Workers/ExtractionWorker.cs
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
using System.Linq;
using System.Collections.Generic;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;

namespace EvnHanoi.DigitizationService.Workers
{
    public class ExtractionWorker : BackgroundService
    {
        private readonly ILogger<ExtractionWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnection _connection;
        private IChannel? _channel;
        private readonly string _llmServerUrl;

        public ExtractionWorker(
            ILogger<ExtractionWorker> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            IConnection connection)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _connection = connection;
            _llmServerUrl = _configuration["AIModelServers:LlmServerUrl"] ?? "http://localhost:8080";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                var exchangeName = "digitization.topic";
                var queueName = "extraction_task_queue";
                var routingKey = "extraction.process.task";

                await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Nhận yêu cầu Extraction: {Message}", messageText);

                    try
                    {
                        var taskMsg = JsonSerializer.Deserialize<ExtractionTaskMessage>(messageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (taskMsg != null)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var repository = scope.ServiceProvider.GetRequiredService<IFileAttachmentRepository>();
                            var minioService = scope.ServiceProvider.GetRequiredService<IMinioStorageService>();
                            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

                            // 1. Cập nhật trạng thái DB
                            try {
                                await repository.UpdateStatusAsync(taskMsg.FileId, "Extracting");
                                _logger.LogInformation("Đã cập nhật trạng thái FileAttachment {FileId} thành Extracting.", taskMsg.FileId);
                            } catch (Exception ex) {
                                _logger.LogWarning("Bỏ qua lỗi DB khi update trạng thái: {Message}", ex.Message);
                            }

                            // 2. Tải file PDF 2 lớp từ MinIO
                            _logger.LogInformation("Tải file PDF {FilePath} từ bucket {BucketName}", taskMsg.FilePath, taskMsg.BucketName);
                            using var fileStream = await minioService.DownloadFileAsync(taskMsg.BucketName, taskMsg.FilePath);
                            
                            using var msPdf = new MemoryStream();
                            await fileStream.CopyToAsync(msPdf, stoppingToken);
                            msPdf.Position = 0;

                            // 3. Chuẩn bị LLM Client
                            var httpClient = httpClientFactory.CreateClient("LlmClient");
                            httpClient.BaseAddress = new Uri(_llmServerUrl);
                            httpClient.Timeout = TimeSpan.FromMinutes(10);

                            // 4. Build Prompt từ Forms
                            string systemPrompt = "";
                            if (taskMsg.Forms != null && taskMsg.Forms.Count > 0)
                            {
                                var fieldsList = new List<string>();
                                foreach (var form in taskMsg.Forms)
                                {
                                    if (form.Fields != null)
                                    {
                                        foreach (var field in form.Fields)
                                        {
                                            fieldsList.Add($"- {field.FieldName}: {field.Description}");
                                        }
                                    }
                                }
                                string fieldsStr = string.Join("\n", fieldsList);
                                systemPrompt = $@"Bạn là chuyên gia trích xuất dữ liệu JSON. Chỉ trả về chuỗi JSON duy nhất, định dạng mảng hoặc object tùy nội dung, không thêm giải thích hoặc định dạng markdown. Trích xuất các trường thông tin sau:
{fieldsStr}";
                            }
                            else
                            {
                                // Default prompt nếu không truyền Forms (Fallback)
                                systemPrompt = @"Bạn là chuyên gia trích xuất dữ liệu. Hãy đọc văn bản và trích xuất thông tin dưới dạng JSON. Chỉ trả về chuỗi JSON duy nhất, không thêm giải thích.";
                            }

                            var finalResults = new List<object>();

                            // 5. Mở PDF bằng PdfPig và trích xuất từng trang
                            using (var document = UglyToad.PdfPig.PdfDocument.Open(msPdf))
                            {
                                int totalPages = document.NumberOfPages;
                                _logger.LogInformation("PDF có {TotalPages} trang. Bắt đầu đọc text và gửi lên LLM.", totalPages);

                                for (int i = 1; i <= totalPages; i++)
                                {
                                    var page = document.GetPage(i);
                                    string pageText = page.Text;

                                    if (string.IsNullOrWhiteSpace(pageText))
                                    {
                                        _logger.LogInformation("Trang {Page} không có text.", i);
                                        continue;
                                    }

                                    _logger.LogInformation("Đang gửi văn bản OCR trang {Page}/{TotalPages} tới llm_server...", i, totalPages);
                                    
                                    string prompt = $"{systemPrompt}\n\nVĂN BẢN OCR:\n{pageText}";

                                    var payload = new 
                                    { 
                                        messages = new[] { new { role = "user", content = prompt } },
                                        temperature = 0.0,
                                        max_tokens = 2000
                                    };
                                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                                    try
                                    {
                                        var response = await httpClient.PostAsync("/v1/chat/completions", content, stoppingToken);
                                        response.EnsureSuccessStatusCode();
                                        var resultStr = await response.Content.ReadAsStringAsync(stoppingToken);
                                        
                                        var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(resultStr);
                                        var extractedJson = jsonNode?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
                                        
                                        try 
                                        {
                                            if (!string.IsNullOrEmpty(extractedJson))
                                            {
                                                if (extractedJson.StartsWith("```json"))
                                                {
                                                    extractedJson = extractedJson.Substring(7);
                                                    if (extractedJson.EndsWith("```")) extractedJson = extractedJson.Substring(0, extractedJson.Length - 3);
                                                }
                                                else if (extractedJson.StartsWith("```"))
                                                {
                                                    extractedJson = extractedJson.Substring(3);
                                                    if (extractedJson.EndsWith("```")) extractedJson = extractedJson.Substring(0, extractedJson.Length - 3);
                                                }

                                                var parsedJson = System.Text.Json.Nodes.JsonNode.Parse(extractedJson.Trim());
                                                finalResults.Add(new { page = i, data = parsedJson });
                                            }
                                        }
                                        catch 
                                        {
                                            finalResults.Add(new { page = i, data_text = extractedJson });
                                        }
                                        
                                        _logger.LogInformation("Hoàn thành Trích xuất trang {Page}.", i);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Lỗi khi gọi llm_server cho trang {Page}.", i);
                                        finalResults.Add(new { page = i, error = ex.Message });
                                    }
                                }
                            }

                            // 6. Lưu file JSON tổng hợp lên MinIO
                            var finalJsonString = JsonSerializer.Serialize(finalResults, new JsonSerializerOptions { WriteIndented = true });
                            _logger.LogInformation("Kết quả trích xuất JSON cho {FileId}:\n{FinalJson}", taskMsg.FileId, finalJsonString);

                            using var resultStream = new MemoryStream(Encoding.UTF8.GetBytes(finalJsonString));
                            var resultFileName = $"extraction_result_{taskMsg.FileId}.json";
                            await minioService.UploadFileAsync(taskMsg.BucketName, resultFileName, resultStream, "application/json");
                            _logger.LogInformation("Đã lưu kết quả gộp vào MinIO: {FileName} tại bucket {BucketName}", resultFileName, taskMsg.BucketName);

                            // 7. Cập nhật trạng thái sau khi lưu DB
                            try {
                                await repository.UpdateStatusAsync(taskMsg.FileId, "Completed");
                                _logger.LogInformation("AI Extraction đã xong. Cập nhật trạng thái FileAttachment {FileId} thành Completed.", taskMsg.FileId);
                            } catch (Exception ex) {
                                _logger.LogWarning("Bỏ qua lỗi DB khi update trạng thái: {Message}", ex.Message);
                            }

                            // Nếu cần publish kết quả sang queue khác (ví dụ cho hệ thống Core lưu DB), thì gọi publisher ở đây
                            // await publisher.PublishMessageAsync(finalResults, "digitization.topic", "extraction.completed");

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
                        _logger.LogError(ex, "Lỗi khi xử lý Extraction task.");
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
                _logger.LogError(ex, "Lỗi kết nối hoặc consume Extraction tasks từ RabbitMQ.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null) await _channel.CloseAsync(cancellationToken: cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
