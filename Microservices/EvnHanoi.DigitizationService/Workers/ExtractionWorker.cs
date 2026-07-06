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
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.DigitizationService.Helpers;

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

                            // 2. Lấy danh sách nội dung text từng trang
                            var pageTexts = new List<string>();

                            // 2a. Thử tải file JSON từ MinIO trước
                            string baseFilePath = taskMsg.FilePath;
                            if (baseFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                baseFilePath = baseFilePath.Substring(0, baseFilePath.Length - 4);
                            }

                            _logger.LogInformation("Tải các file JSON với base {BaseFilePath} từ bucket {BucketName}", baseFilePath, taskMsg.BucketName);
                            int pageNumToFetch = 1;
                            while (!stoppingToken.IsCancellationRequested)
                            {
                                string jsonFileName = $"{baseFilePath}_page_{pageNumToFetch}.json";
                                try
                                {
                                    using var jsonStream = await minioService.DownloadFileAsync(taskMsg.BucketName, jsonFileName);
                                    using var reader = new StreamReader(jsonStream, Encoding.UTF8);
                                    string jsonText = await reader.ReadToEndAsync();

                                    if (OcrPageContentHelper.IsEmptyOcrJson(jsonText))
                                    {
                                        pageTexts.Add("[]");
                                        pageNumToFetch++;
                                        continue;
                                    }

                                    var boxes = JsonNode.Parse(jsonText)?.AsArray();
                                    if (boxes != null)
                                    {
                                        var compactBoxes = new JsonArray();
                                        foreach (var box in boxes)
                                        {
                                            var boxArr = box?["box"]?.AsArray() ?? box?["Box"]?.AsArray();
                                            var text = OcrPageContentHelper.NormalizeUtf8Text(
                                                box?["text"]?.GetValue<string>() ?? box?["Text"]?.GetValue<string>());
                                            if (boxArr != null && boxArr.Count == 4 && !string.IsNullOrWhiteSpace(text))
                                            {
                                                compactBoxes.Add(new JsonObject
                                                {
                                                    ["Text"] = text,
                                                    ["Box"] = new JsonArray(
                                                        Math.Round(boxArr[0]!.GetValue<float>()),
                                                        Math.Round(boxArr[1]!.GetValue<float>()),
                                                        Math.Round(boxArr[2]!.GetValue<float>()),
                                                        Math.Round(boxArr[3]!.GetValue<float>())
                                                    )
                                                });
                                            }
                                        }
                                        pageTexts.Add(compactBoxes.Count > 0
                                            ? compactBoxes.ToJsonString(OcrPageContentHelper.Utf8JsonOptions)
                                            : "[]");
                                    }
                                    else
                                    {
                                        pageTexts.Add("[]");
                                    }
                                    pageNumToFetch++;
                                }
                                catch (Exception)
                                {
                                    _logger.LogInformation("Kết thúc tải file JSON tại trang {Page}.", pageNumToFetch);
                                    break;
                                }
                            }

                            // 2b. Fallback — nếu không có JSON hoặc toàn bộ JSON rỗng, đọc PDF bằng PdfPig
                            var needsPdfPigFallback = pageTexts.Count == 0
                                || pageTexts.All(OcrPageContentHelper.IsEmptyOcrJson);
                            if (needsPdfPigFallback)
                            {
                                pageTexts.Clear();
                                _logger.LogWarning(
                                    "File JSON OCR rỗng hoặc không tồn tại. Fallback: đọc text trực tiếp từ PDF bằng PdfPig.");
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
                                        string rawText = OcrPageContentHelper.NormalizeUtf8Text(page.Text);
                                        
                                        // Tạo mảng JSON giả với 1 box toàn trang để tương thích với LLM prompt mới
                                        var fallbackBox = new JsonArray
                                        {
                                            new JsonObject
                                            {
                                                ["Text"] = rawText,
                                                ["Box"] = new JsonArray(0, 0, 1000, 1000)
                                            }
                                        };
                                        pageTexts.Add(fallbackBox.ToJsonString(OcrPageContentHelper.Utf8JsonOptions));
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
                                _logger.LogWarning("Không tìm thấy nội dung OCR/JSON nào cho {FilePath}", taskMsg.FilePath);
                                // Gửi bản tin thông báo hoàn thành với Status = Failed lên RabbitMQ
                                var failedCompletedMsg = new
                                {
                                    FileId = taskMsg.FileId,
                                    Action = "extraction.process.completed",
                                    ResultFile = (string?)null,
                                    BucketName = taskMsg.BucketName,
                                    Status = "Failed"
                                };
                                await publisher.PublishMessageAsync(failedCompletedMsg, "digitization.topic", "extraction.process.completed");
                                _logger.LogInformation("Đã gửi bản tin báo lỗi (Failed) cho {FileId} do không có nội dung OCR.", taskMsg.FileId);

                                // Tự động ack message nếu file không có để tránh kẹt queue
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
Nhiệm vụ của bạn là đọc danh sách các khối chữ (JSON boxes) từ kết quả OCR và trích xuất CHÍNH XÁC các trường thông tin được yêu cầu dưới định dạng JSON object.

ĐẦU VÀO CỦA BẠN:
Là một mảng JSON chứa các đối tượng có cấu trúc: {{""Text"": ""nội dung"", ""Box"": [x0, y0, x1, y1]}}.
- x0, y0 là tọa độ góc trên bên trái; x1, y1 là tọa độ góc dưới bên phải.
- Bạn HÃY hình dung bố cục trang giấy dựa trên tọa độ: x0 gần nhau là cùng cột, y0 gần nhau là cùng hàng.

NGUYÊN TẮC QUAN TRỌNG:
1. SỬ DỤNG TƯ DUY KHÔNG GIAN: Dựa vào toạ độ Box để tránh ghép nhầm văn bản của cột trái và cột phải vào cùng một trường. Chỉ lấy giá trị cốt lõi, loại bỏ các chữ nhiễu ở cột bên cạnh (như Quốc hiệu, Tiêu ngữ, Ngày tháng).
2. TỰ ĐỘNG SỬA LỖI CHÍNH TẢ OCR: Khi trích xuất văn bản, hãy tự động sửa các lỗi chính tả do OCR gây ra dựa vào ngữ cảnh. 
   - Lỗi dấu thanh (KỶ→KỸ, SỰA→SỬA, TÍCH→TỊCH)
   - Lỗi mất dấu (son→sơn, gi→gỉ)
   - Lỗi nhận diện (UỞ BAN→ỦY BAN, UỬ→ỦY, Trưởng→Trường tuỳ ngữ cảnh).
   - GIỮ NGUYÊN các mã kỹ thuật, số liệu (22/0,4kV, TBA, QLĐT).
3. NẾU KHÔNG TÌM THẤY thông tin cho một trường, bắt buộc trả về giá trị null cho trường đó, tuyệt đối không điền 'Không có' hay 'N/A'.
4. BẮT BUỘC TRẢ VỀ JSON HỢP LỆ (VALID JSON). Phải kiểm tra kỹ việc đóng ngoặc kép (dấu """") đối với các giá trị chuỗi dài. CHỈ TRẢ VỀ một chuỗi JSON duy nhất, KHÔNG thêm giải thích hay markdown.
5. Format JSON phải tuân thủ nghiêm ngặt theo cấu trúc đã cho, với tên trường chính xác như yêu cầu. KHÔNG được thêm bớt hay đổi tên trường.
6. GHÉP CÁC DÒNG LIÊN TIẾP: Nếu một trường có nhiều dòng liền kề nhau (các box có y0 liên tiếp, cùng vùng x), hãy ghép tất cả thành 1 giá trị. Ví dụ: ""KT. CHỦ TỊCH"" và ""PHÓ CHỦ TỊCH"" trên 2 dòng → ghép thành ""KT. CHỦ TỊCH\nPHÓ CHỦ TỊCH"".
7. PHÂN BIỆT KHU VỰC VĂN BẢN:
   - Phần ""Nơi nhận"" (thường ở góc dưới bên trái, bắt đầu bằng ""Nơi nhận:"") KHÔNG phải là người ký.
   - Phần ""Người ký"" thường nằm ở góc dưới bên phải, phía trên tên người ký có chức danh.
   - Phần ""Trích yếu"" nằm ở header (giữa trang, dưới tên loại văn bản), KHÔNG phải nội dung chi tiết của các điều khoản.
8. TRƯỜNG ĐỂ TRỐNG: Nếu phát hiện vị trí có dấu hiệu để trống (ví dụ: ""Số: .../QĐ-UBND"", ""ngày ... tháng ... năm"") thì trả về giá trị null.
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
                                        pageText = OcrPageContentHelper.NormalizeUtf8Text(pageText);

                                        // Đọc cấu hình extraction từ appsettings
                                        int maxTokens = _configuration.GetValue("Extraction:MaxTokens", 4096);
                                        float llmTemperature = _configuration.GetValue("Extraction:Temperature", 0.05f);
                                        int maxRetries = _configuration.GetValue("Extraction:MaxRetries", 2);
                                        int retryDelayMs = _configuration.GetValue("Extraction:RetryDelayMs", 1000);

                                        object pageResult = null;
                                        var swTotal = Stopwatch.StartNew();

                                        for (int attempt = 0; attempt <= maxRetries; attempt++)
                                        {
                                            if (attempt > 0)
                                            {
                                                _logger.LogWarning("Retry lần {Attempt}/{MaxRetries} cho trang {Page}.", attempt, maxRetries, pageNum);
                                                await Task.Delay(retryDelayMs * attempt, stoppingToken);
                                            }

                                            var payload = new
                                            {
                                                messages = new object[]
                                                {
                                                    new { role = "system", content = systemPrompt },
                                                    new { role = "user", content = $"VĂN BẢN OCR:\n{pageText}" }
                                                },
                                                temperature = llmTemperature,
                                                max_tokens = maxTokens
                                            };
                                            var content = new StringContent(
                                                JsonSerializer.Serialize(payload, OcrPageContentHelper.Utf8JsonOptions),
                                                Encoding.UTF8,
                                                "application/json");

                                            var sw = Stopwatch.StartNew();
                                            var response = await httpClient.PostAsync("/v1/chat/completions", content, stoppingToken);
                                            response.EnsureSuccessStatusCode();
                                            var resultStr = await response.Content.ReadAsStringAsync(stoppingToken);
                                            sw.Stop();

                                            var jsonNode = JsonNode.Parse(resultStr);
                                            var extractedJson = jsonNode?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();

                                            // Diagnostic: log chi tiết khi LLM response rỗng
                                            if (string.IsNullOrEmpty(extractedJson))
                                            {
                                                _logger.LogWarning(
                                                    "[DIAGNOSTIC] LLM response content rỗng cho trang {Page} (attempt {Attempt}). " +
                                                    "HTTP Status: {Status}. Response length: {Length} bytes.",
                                                    pageNum, attempt, response.StatusCode, resultStr?.Length ?? 0);
                                                if (attempt < maxRetries) continue; // Retry
                                                pageResult = new { page = pageNum, data_text = "" };
                                                break;
                                            }

                                            // Strip markdown code block markers
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

                                            try
                                            {
                                                var parsedJson = JsonNode.Parse(extractedJson.Trim());
                                                _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành Trích xuất sau {ElapsedMs} ms.", pageNum, sw.ElapsedMilliseconds);
                                                pageResult = new { page = pageNum, data = parsedJson };
                                                break; // Thành công → thoát retry
                                            }
                                            catch
                                            {
                                                _logger.LogWarning("JSON parse lỗi trang {Page} (attempt {Attempt}): {Json}",
                                                    pageNum, attempt, extractedJson?.Length > 300 ? extractedJson[..300] + "..." : extractedJson);

                                                if (attempt < maxRetries) continue; // Retry trước khi fallback

                                                // Cứu hộ JSON bị lỗi (thiếu ngoặc kép, sai cú pháp) bằng Regex — chỉ khi hết retry
                                                try
                                                {
                                                    var dict = new Dictionary<string, object>();
                                                    bool rescued = false;
                                                    if (taskMsg.Form?.Fields != null)
                                                    {
                                                        var keys = taskMsg.Form.Fields.Select(f => f.FieldName).ToList();
                                                        for (int k = 0; k < keys.Count; k++)
                                                        {
                                                            string key = keys[k];
                                                            string nextKey = k < keys.Count - 1 ? keys[k + 1] : null;

                                                            var match = Regex.Match(extractedJson, $"\"{key}\"\\s*:\\s*");
                                                            if (match.Success)
                                                            {
                                                                int startValIdx = match.Index + match.Length;
                                                                int endValIdx = extractedJson.Length;

                                                                if (nextKey != null)
                                                                {
                                                                    var nextMatch = Regex.Match(extractedJson, $"\"{nextKey}\"\\s*:\\s*");
                                                                    if (nextMatch.Success)
                                                                    {
                                                                        endValIdx = nextMatch.Index;
                                                                        int commaIdx = extractedJson.LastIndexOf(',', endValIdx - 1, endValIdx - startValIdx);
                                                                        if (commaIdx != -1) endValIdx = commaIdx;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    int braceIdx = extractedJson.LastIndexOf('}');
                                                                    if (braceIdx > startValIdx) endValIdx = braceIdx;
                                                                }

                                                                if (endValIdx > startValIdx)
                                                                {
                                                                    string rawVal = extractedJson.Substring(startValIdx, endValIdx - startValIdx).Trim();
                                                                    if (rawVal == "null") dict[key] = null;
                                                                    else
                                                                    {
                                                                        if (rawVal.StartsWith("\"")) rawVal = rawVal.Substring(1);
                                                                        if (rawVal.EndsWith("\"")) rawVal = rawVal.Substring(0, rawVal.Length - 1);
                                                                        rawVal = rawVal.Replace("\\\"", "\"").Replace("\\n", "\n");
                                                                        dict[key] = rawVal.Trim();
                                                                    }
                                                                    rescued = true;
                                                                }
                                                            }
                                                        }
                                                    }

                                                    if (rescued && dict.Count > 0)
                                                    {
                                                        var rescuedJson = JsonSerializer.Serialize(dict);
                                                        var parsedJson = JsonNode.Parse(rescuedJson);
                                                        _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành Trích xuất (ĐÃ CỨU HỘ BẰNG REGEX) sau {ElapsedMs} ms.", pageNum, sw.ElapsedMilliseconds);
                                                        pageResult = new { page = pageNum, data = parsedJson };
                                                        break;
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.LogWarning(ex, "Lỗi khi chạy cứu hộ JSON cho trang {Page}", pageNum);
                                                }

                                                // Fallback cuối cùng
                                                _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành Trích xuất (Raw Text) sau {ElapsedMs} ms.", pageNum, swTotal.ElapsedMilliseconds);
                                                pageResult = new { page = pageNum, data_text = extractedJson };
                                                break;
                                            }
                                        }

                                        return pageResult ?? new { page = pageNum, data_text = "" };
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
                                    // Xử lý data_text: nếu trang không có "data" nhưng có "data_text" chứa JSON hợp lệ
                                    else
                                    {
                                        var dataTextNode = dataNode?["data_text"];
                                        if (dataTextNode != null)
                                        {
                                            string rawText = dataTextNode.GetValue<string>();
                                            if (!string.IsNullOrWhiteSpace(rawText))
                                            {
                                                try
                                                {
                                                    // Thử strip markdown + parse JSON từ data_text
                                                    string cleaned = rawText.Trim();
                                                    if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
                                                    if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
                                                    if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];

                                                    var parsedDataText = JsonNode.Parse(cleaned.Trim());
                                                    if (parsedDataText is JsonObject textPageData)
                                                    {
                                                        foreach (var kvp in textPageData)
                                                        {
                                                            if (!mergedResult.ContainsKey(kvp.Key))
                                                                mergedResult[kvp.Key] = kvp.Value != null ? JsonNode.Parse(kvp.Value.ToJsonString()) : null;
                                                            else if (mergedResult[kvp.Key] == null && kvp.Value != null)
                                                                mergedResult[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                                                        }
                                                        _logger.LogInformation("Đã merge thành công data_text từ trang.");
                                                    }
                                                }
                                                catch (Exception dtEx)
                                                {
                                                    _logger.LogWarning("Không parse được data_text: {Error}", dtEx.Message);
                                                }
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

                            bool allFailed = true;
                            if (resultsArray.Length > 0)
                            {
                                foreach (var r in resultsArray)
                                {
                                    if (r != null)
                                    {
                                        try
                                        {
                                            var errorProp = r.GetType().GetProperty("error");
                                            if (errorProp == null || errorProp.GetValue(r) == null)
                                            {
                                                allFailed = false;
                                                break;
                                            }
                                        }
                                        catch
                                        {
                                            allFailed = false;
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                allFailed = true;
                            }

                            string status = allFailed ? "Failed" : "Success";

                            // 7. Gửi bản tin báo hoàn thành (Completed) lên RabbitMQ
                            var completedMsg = new
                            {
                                FileId = taskMsg.FileId,
                                Action = "extraction.process.completed",
                                ResultFile = resultFileName,
                                BucketName = taskMsg.BucketName,
                                Status = status
                            };
                            await publisher.PublishMessageAsync(completedMsg, "digitization.topic", "extraction.process.completed");
                            _logger.LogInformation("Đã gửi bản tin hoàn thành lên RabbitMQ (Routing key: extraction.process.completed) với Status: {Status}.", status);

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
