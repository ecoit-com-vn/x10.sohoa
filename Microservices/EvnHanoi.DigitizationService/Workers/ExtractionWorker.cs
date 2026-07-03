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
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
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
                            var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

                            // 1. Cập nhật trạng thái DB
                            try
                            {
                                await repository.UpdateStatusAsync(taskMsg.FileId, "Extracting");
                                _logger.LogInformation("Đã cập nhật trạng thái FileAttachment {FileId} thành Extracting.", taskMsg.FileId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Bỏ qua lỗi DB khi update trạng thái: {Message}", ex.Message);
                            }

                            // 2. Lấy danh sách nội dung text từng trang
                            var pageTexts = new List<string>();

                            // 2a. Thử tải file Markdown từ MinIO trước
                            string baseFilePath = taskMsg.FilePath;
                            if (baseFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                baseFilePath = baseFilePath.Substring(0, baseFilePath.Length - 4);
                            }

                            _logger.LogInformation("Tải các file Markdown với base {BaseFilePath} từ bucket {BucketName}", baseFilePath, taskMsg.BucketName);
                            int pageNumToFetch = 1;
                            while (!stoppingToken.IsCancellationRequested)
                            {
                                string mdFileName = $"{baseFilePath}_page_{pageNumToFetch}.md";
                                try
                                {
                                    using var mdStream = await minioService.DownloadFileAsync(taskMsg.BucketName, mdFileName);
                                    using var reader = new StreamReader(mdStream, Encoding.UTF8);
                                    string text = await reader.ReadToEndAsync();
                                    pageTexts.Add(text);
                                    pageNumToFetch++;
                                }
                                catch (Exception)
                                {
                                    _logger.LogInformation("Kết thúc tải file Markdown tại trang {Page}.", pageNumToFetch);
                                    break;
                                }
                            }

                            // 2b. Fix 3: Fallback — nếu không tìm thấy file .md, đọc PDF bằng PdfPig
                            if (pageTexts.Count == 0)
                            {
                                _logger.LogWarning("Không tìm thấy file Markdown nào. Fallback: đọc text trực tiếp từ PDF bằng PdfPig.");
                                try
                                {
                                    using var fileStream = await minioService.DownloadFileAsync(taskMsg.BucketName, taskMsg.FilePath);
                                    using var msPdf = new MemoryStream();
                                    await fileStream.CopyToAsync(msPdf, stoppingToken);
                                    msPdf.Position = 0;

                                    using var document = UglyToad.PdfPig.PdfDocument.Open(msPdf);
                                    for (int p = 1; p <= document.NumberOfPages; p++)
                                    {
                                        var page = document.GetPage(p);
                                        pageTexts.Add(page.Text ?? "");
                                    }
                                    _logger.LogInformation("Fallback PdfPig: đọc được {TotalPages} trang.", pageTexts.Count);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Lỗi fallback PdfPig khi đọc PDF {FilePath}.", taskMsg.FilePath);
                                }
                            }

                            int totalPages = pageTexts.Count;
                            _logger.LogInformation("Tổng cộng tìm thấy {TotalPages} trang text.", totalPages);

                            if (totalPages == 0)
                            {
                                _logger.LogWarning("Không tìm thấy dữ liệu text nào cho {FilePath}. Bỏ qua extraction.", taskMsg.FilePath);
                                await _channel.BasicAckAsync(ea.DeliveryTag, false);
                                return;
                            }

                            // 3. Chuẩn bị LLM Client
                            var httpClient = httpClientFactory.CreateClient("LlmClient");
                            httpClient.BaseAddress = new Uri(_llmServerUrl);
                            httpClient.Timeout = TimeSpan.FromMinutes(10);

                            // 4. Build Prompt từ Forms
                            string systemPrompt = "";
                            if (taskMsg.Form != null)
                            {
                                var fieldsList = new List<string>();
                                if (taskMsg.Form.Fields != null)
                                {
                                    foreach (var field in taskMsg.Form.Fields)
                                    {
                                        fieldsList.Add($"- {field.FieldName}: {field.Description}");
                                    }
                                }
                                string fieldsStr = string.Join("\n", fieldsList);

                                systemPrompt = $@"Bạn là một chuyên gia phân tích và trích xuất dữ liệu tài liệu kỹ thuật ngành điện lực Việt Nam.
Nhiệm vụ của bạn là đọc kỹ văn bản OCR và trích xuất CHÍNH XÁC các trường thông tin được yêu cầu dưới định dạng JSON object.

NGUYÊN TẮC QUAN TRỌNG:
1. TRÍCH XUẤT CHÍNH XÁC từng từ từ văn bản, KHÔNG ĐƯỢC suy đoán, tóm tắt hay tự bịa ra thông tin.
2. NẾU KHÔNG TÌM THẤY thông tin cho một trường, bắt buộc trả về giá trị null cho trường đó, tuyệt đối không điền 'Không có' hay 'N/A'.
3. CHỈ TRẢ VỀ một chuỗi JSON duy nhất, định dạng object. KHÔNG thêm bất kỳ lời giải thích, mở bài hay markdown nào khác.
4. Format JSON phải tuân thủ nghiêm ngặt theo cấu trúc đã cho, với tên trường chính xác như yêu cầu. KHÔNG được thêm bớt hay đổi tên trường.
{taskMsg.ExtractPrompt}

CÁC TRƯỜNG CẦN TRÍCH XUẤT:
{fieldsStr}";
                            }
                            else
                            {
                                // Default prompt nếu không truyền Forms (Fallback)
                                systemPrompt = @"Bạn là chuyên gia trích xuất dữ liệu. Hãy đọc văn bản và trích xuất thông tin dưới dạng JSON. Chỉ trả về chuỗi JSON duy nhất, không thêm giải thích.";
                            }

                            // 5. Gửi từng trang lên LLM
                            var tasks = new List<Task<object>>();
                            List<object> finalResults = new List<object>();

                            using var semaphore = new SemaphoreSlim(1, 1);
                            int completedPages = 0;

                            for (int i = 0; i < totalPages; i++)
                            {
                                string pageText = pageTexts[i];
                                int pageNum = i + 1;

                                if (string.IsNullOrWhiteSpace(pageText))
                                {
                                    _logger.LogInformation("Trang {Page} không có text. Bỏ qua.", pageNum);
                                    continue;
                                }

                                // Local function to handle each page asynchronously
                                async Task<object> ProcessPageAsync()
                                {
                                    await semaphore.WaitAsync(stoppingToken);
                                    try
                                    {
                                        _logger.LogInformation("Đang gửi văn bản trang {Page}/{TotalPages} tới llm_server...", pageNum, totalPages);

                                        string prompt = $"{systemPrompt}\n\nVĂN BẢN OCR:\n{pageText}";

                                        var payload = new
                                        {
                                            messages = new[] { new { role = "user", content = prompt } },
                                            temperature = 0.0,
                                            max_tokens = 2000
                                        };
                                        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                                        var sw = Stopwatch.StartNew();
                                        var response = await httpClient.PostAsync("/v1/chat/completions", content, stoppingToken);
                                        response.EnsureSuccessStatusCode();
                                        var resultStr = await response.Content.ReadAsStringAsync(stoppingToken);
                                        sw.Stop();

                                        var jsonNode = JsonNode.Parse(resultStr);
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

                                                var parsedJson = JsonNode.Parse(extractedJson.Trim());
                                                _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành Trích xuất sau {ElapsedMs} ms.", pageNum, sw.ElapsedMilliseconds);
                                                return new { page = pageNum, data = parsedJson };
                                            }
                                            return new { page = pageNum, data_text = extractedJson };
                                        }
                                        catch
                                        {
                                            _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành Trích xuất (Raw Text) sau {ElapsedMs} ms.", pageNum, sw.ElapsedMilliseconds);
                                            return new { page = pageNum, data_text = extractedJson };
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Lỗi khi gọi llm_server cho trang {Page}.", pageNum);
                                        return new { page = pageNum, error = ex.Message };
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                        int currentCompleted = Interlocked.Increment(ref completedPages);
                                        var progressMsg = new
                                        {
                                            FileId = taskMsg.FileId,
                                            Action = "extraction.process.progress",
                                            CurrentPage = currentCompleted,
                                            TotalPages = totalPages,
                                            Progress = (int)Math.Round((double)currentCompleted / totalPages * 100)
                                        };
                                        await publisher.PublishMessageAsync(progressMsg, "digitization.topic", "extraction.process.progress");
                                    }
                                }

                                tasks.Add(ProcessPageAsync());
                            }

                            // Đợi tất cả các task hoàn thành
                            var resultsArray = await Task.WhenAll(tasks);
                            finalResults = resultsArray.Where(r => r != null).OrderBy(r => ((dynamic)r).page).ToList();

                            // Fix 1: Merge thông minh — gộp kết quả tất cả trang thành 1 JSON object
                            // Ưu tiên giá trị non-null đầu tiên tìm thấy cho mỗi trường
                            var mergedResult = new JsonObject();
                            foreach (var result in finalResults)
                            {
                                try
                                {
                                    var dynamicResult = (dynamic)result;
                                    // Chỉ merge các kết quả có property "data" (JsonNode)
                                    var dataJson = JsonSerializer.Serialize(result);
                                    var dataNode = JsonNode.Parse(dataJson);
                                    var dataObj = dataNode?["data"];

                                    if (dataObj is JsonObject pageData)
                                    {
                                        foreach (var kvp in pageData)
                                        {
                                            string fieldName = kvp.Key;
                                            var fieldValue = kvp.Value;

                                            // Chỉ ghi đè nếu trường chưa có giá trị hoặc giá trị hiện tại là null
                                            if (!mergedResult.ContainsKey(fieldName))
                                            {
                                                // Clone giá trị để tránh lỗi "node already has a parent"
                                                mergedResult[fieldName] = fieldValue != null ? JsonNode.Parse(fieldValue.ToJsonString()) : null;
                                            }
                                            else if (mergedResult[fieldName] == null && fieldValue != null)
                                            {
                                                mergedResult[fieldName] = JsonNode.Parse(fieldValue.ToJsonString());
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Không thể merge kết quả trang: {Error}", ex.Message);
                                }
                            }

                            _logger.LogInformation("Merge thông minh hoàn tất: {FieldCount} trường.", mergedResult.Count);

                            // 6. Lưu file JSON tổng hợp lên MinIO
                            // Lưu cả kết quả per-page và kết quả merged
                            var outputPayload = new
                            {
                                merged = mergedResult,
                                pages = finalResults
                            };
                            var finalJsonString = JsonSerializer.Serialize(outputPayload, new JsonSerializerOptions { WriteIndented = true });

                            using var resultStream = new MemoryStream(Encoding.UTF8.GetBytes(finalJsonString));
                            string directory = Path.GetDirectoryName(taskMsg.FilePath)?.Replace("\\", "/") ?? string.Empty;
                            var resultFileName = string.IsNullOrEmpty(directory) 
                                ? $"extraction_result_{taskMsg.FileId}.json" 
                                : $"{directory}/extraction_result_{taskMsg.FileId}.json";
                            await minioService.UploadFileAsync(taskMsg.BucketName, resultFileName, resultStream, "application/json");
                            _logger.LogInformation("Đã lưu kết quả gộp vào MinIO: {FileName} tại bucket {BucketName}", resultFileName, taskMsg.BucketName);

                            // 7. Gửi bản tin báo hoàn thành (Completed) lên RabbitMQ
                            var completedMsg = new
                            {
                                FileId = taskMsg.FileId,
                                Action = "extraction.process.completed",
                                ResultFile = resultFileName,
                                BucketName = taskMsg.BucketName,
                                Status = "Success"
                            };
                            await publisher.PublishMessageAsync(completedMsg, "digitization.topic", "extraction.process.completed");
                            _logger.LogInformation("Đã gửi bản tin hoàn thành lên RabbitMQ (Routing key: extraction.process.completed).");

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
