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
using EvnHanoi.Infrastructure.Messaging;

namespace EvnHanoi.DigitizationService.Workers
{
    public record PageResult(int Page, JsonNode? Data, string? DataText, string? Error, string? ExtractionMethod);

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

                // Giới hạn số message xử lý đồng thời/1 kết nối bằng đúng ConsumerDispatchConcurrency
                // (xem Program.cs) — để RabbitMQ không dồn quá nhiều message chưa ack cho worker.
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 4, global: false, cancellationToken: stoppingToken);

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
                                        var compactBoxes = new StringBuilder();
                                        foreach (var box in boxes)
                                        {
                                            var boxArr = box?["box"]?.AsArray() ?? box?["Box"]?.AsArray();
                                            var text = OcrPageContentHelper.NormalizeUtf8Text(
                                                box?["text"]?.GetValue<string>() ?? box?["Text"]?.GetValue<string>());
                                            if (boxArr != null && boxArr.Count == 4 && !string.IsNullOrWhiteSpace(text))
                                            {
                                                compactBoxes.AppendLine($"[{Math.Round(boxArr[0]!.GetValue<float>())}, {Math.Round(boxArr[1]!.GetValue<float>())}, {Math.Round(boxArr[2]!.GetValue<float>())}, {Math.Round(boxArr[3]!.GetValue<float>())}] {text}");
                                            }
                                        }
                                        pageTexts.Add(compactBoxes.ToString().Trim());
                                    }
                                    else
                                    {
                                        pageTexts.Add("");
                                    }
                                    pageNumToFetch++;
                                }
                                catch (Exception ex)
                                {
                                    if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogInformation("Kết thúc tải file JSON tại trang {Page} (File không tồn tại).", pageNumToFetch);
                                    }
                                    else
                                    {
                                        _logger.LogWarning(ex, "Lỗi khi tải file JSON tại trang {Page}. Dừng quá trình tải JSON.", pageNumToFetch);
                                    }
                                    break;
                                }
                            }

                            // 2b. Fallback — nếu không có JSON hoặc toàn bộ JSON rỗng, đọc PDF bằng PdfPig
                            var needsPdfPigFallback = pageTexts.Count == 0
                                || pageTexts.All(string.IsNullOrWhiteSpace);
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
                                        
                                        // Tạo text giả với 1 box toàn trang để tương thích với LLM prompt mới
                                        pageTexts.Add($"[0, 0, 1000, 1000] {rawText}");
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

                            // Giới hạn số trang gửi lên LLM theo lựa chọn của người dùng lúc upload.
                            // Bước OCR phía trước KHÔNG bị ảnh hưởng (PDF 2 lớp + file text vẫn đủ trang),
                            // đây chỉ là bước bóc tách — bước tốn thời gian nhất (~30-60s/trang).
                            var pagesToExtract = new HashSet<int>(
                                ExtractionScopes.ResolvePageNumbers(taskMsg.ExtractionScope, totalPages));
                            if (totalPages > 0)
                            {
                                _logger.LogInformation(
                                    "Phạm vi bóc tách: {Scope} -> xử lý {SelectedCount}/{TotalPages} trang ({PageList}).",
                                    ExtractionScopes.Describe(taskMsg.ExtractionScope),
                                    pagesToExtract.Count, totalPages, string.Join(", ", pagesToExtract));
                            }

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
                                // Tín hiệu hoàn thành cuối cùng — thử lại có backoff; nếu vẫn thất bại, vẫn ack
                                // (đã cố hết sức) và để watchdog (dựa ModifiedDate, độc lập RabbitMQ) đóng job.
                                await publisher.TryPublishMessageAsync(failedCompletedMsg, "digitization.topic", "extraction.process.completed",
                                    maxAttempts: 3, initialDelay: TimeSpan.FromSeconds(2));
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
                            string systemPrompt = $@"Bạn là một chuyên gia phân tích và trích xuất dữ liệu tài liệu kỹ thuật ngành điện lực Việt Nam, có khả năng đọc hiểu bố cục trang giấy dựa vào tọa độ của kết quả OCR.
Nhiệm vụ: đọc danh sách các khối chữ OCR được cung cấp bên dưới và trích xuất CHÍNH XÁC các trường thông tin được yêu cầu (danh sách trường và cấu trúc JSON mong muốn được cung cấp kèm theo, ngay sau phần chỉ dẫn này). Trả về kết quả dưới dạng MỘT JSON object DUY NHẤT.

[ĐỊNH DẠNG ĐẦU VÀO]
Đầu vào là danh sách các dòng văn bản, mỗi dòng có cấu trúc:
[x0, y0, x1, y1] nội_dung_text
(x0, y0): tọa độ góc trên-trái của khối chữ. (x1, y1): tọa độ góc dưới-phải.
x0 gần nhau → nhiều khả năng cùng một cột. y0 gần nhau → nhiều khả năng cùng một hàng.
Thứ tự đọc đúng của trang: nhóm các khối theo cột dựa vào x0 trước; trong mỗi cột, sắp xếp theo y0 tăng dần (đọc từ trên xuống dưới); giữa các cột, đọc lần lượt từ trái sang phải.

[QUAN HỆ NHÃN – GIÁ TRỊ (LABEL – VALUE)]
Trong biểu mẫu, văn bản kỹ thuật ngành điện, ""nhãn trường"" (label — thường kết thúc bằng dấu "":"") thường đi kèm giá trị cần trích xuất theo 1 trong 2 kiểu:
CÙNG HÀNG: label bên trái, giá trị là khối ngay bên phải (x1 của label gần x0 của giá trị, y0 hai khối xấp xỉ nhau).
CÙNG CỘT: giá trị nằm ngay dưới label (x0 hai khối xấp xỉ nhau, y0 của giá trị lớn hơn y0 của label một khoảng ngắn).
CHỈ lấy khối chữ gần nhất, hợp lý nhất theo 2 kiểu quan hệ trên. KHÔNG lấy khối chữ ở xa, ở cột/bảng khác dù nội dung nghe có vẻ liên quan — đây là nguyên nhân phổ biến nhất gây ghép nhầm dữ liệu giữa các cột cạnh nhau.

[NGUYÊN TẮC CHỐNG ẢO GIÁC — ƯU TIÊN CAO HƠN MỌI NGUYÊN TẮC KHÁC]

CHỈ điền giá trị THỰC SỰ XUẤT HIỆN trong danh sách khối chữ OCR được cung cấp. TUYỆT ĐỐI KHÔNG suy diễn, không ""đoán"" giá trị hợp lý dựa trên kiến thức nền về ngành điện, không tự bổ sung phần bị thiếu, không tự tính toán lại số liệu không có trong OCR.
Nếu không chắc chắn khối chữ nào là giá trị đúng, hoặc một trường có thể hiểu theo nhiều cách → trả về null. Thà bỏ trống còn hơn điền sai.
KHÔNG tự đổi định dạng số liệu/ngày tháng so với nguyên văn OCR (không thêm/bớt số 0, không đổi thứ tự ngày/tháng/năm, không làm tròn số, không tự quy đổi đơn vị).
KHÔNG thêm bất kỳ trường, ghi chú, độ tin cậy (confidence), hay lời giải thích nào ngoài đúng các trường đã được yêu cầu.

[SỬA LỖI CHÍNH TẢ OCR — CÓ RANH GIỚI RÕ RÀNG]
ĐƯỢC PHÉP tự động sửa lỗi chính tả tiếng Việt rõ ràng do OCR gây ra, dựa vào ngữ cảnh xung quanh. Một số lỗi thường gặp: nhầm dấu thanh do nét mờ/đứt (KỶ→KỸ, SỰA→SỬA, TÍCH→TỊCH...), nhầm ký tự có hình dạng giống nhau (D↔Đ, rn↔m...), dính/tách từ sai do khoảng cách ký tự bất thường khi scan.
KHÔNG áp dụng việc sửa lỗi này cho: mã thiết bị, mã trạm, số công tơ, số hiệu văn bản/quyết định, biển số, hoặc bất kỳ chuỗi ký tự xen kẽ chữ-số/mã định danh kỹ thuật nào. GIỮ NGUYÊN 100% các mã này kể cả khi trông ""bất thường"" — đây thường là định danh duy nhất, tự ý sửa sẽ làm sai lệch dữ liệu nghiêm trọng hơn nhiều so với một lỗi chính tả.

[GHÉP CÁC DÒNG LIÊN TIẾP THÀNH MỘT TRƯỜNG]
Nếu một trường có nội dung trải trên nhiều dòng liền kề (các khối có y0 kế tiếp nhau, cùng thuộc vùng x của một giá trị — ví dụ địa chỉ, diễn giải dài), ghép các dòng đó thành 1 chuỗi duy nhất, nối bằng ký tự xuống dòng \n theo đúng thứ tự y0 tăng dần. Bỏ qua các khối là tiêu đề trang, số trang, chữ ký, con dấu, hoặc chữ nhiễu không thuộc trường nào được yêu cầu.

[TRƯỜNG HỢP KHÔNG TÌM THẤY DỮ LIỆU]
Trường không tìm thấy trong OCR → giá trị JSON null thật sự (KHÔNG phải chuỗi ""null"", không phải ""N/A"", ""Không có"", hay chuỗi rỗng """").
Nếu toàn bộ trang không chứa bất kỳ trường nào được yêu cầu (trang chữ ký, phụ lục, trang trắng...) → VẪN PHẢI trả về đầy đủ JSON object với TẤT CẢ các trường = null. KHÔNG bỏ trống câu trả lời, KHÔNG trả về chuỗi rỗng, KHÔNG trả về mảng rỗng thay cho JSON object.

[ĐỊNH DẠNG JSON ĐẦU RA — BẮT BUỘC TUYỆT ĐỐI]
Câu trả lời CHỈ gồm một JSON object DUY NHẤT: ký tự đầu tiên là {{, ký tự cuối cùng là }}.
TUYỆT ĐỐI KHÔNG bọc JSON trong dấu backtick hay khối mã kiểu markdown (không mở đầu bằng ba dấu backtick kèm chữ json, không kết thúc bằng ba dấu backtick). KHÔNG thêm câu dẫn, lời giải thích, hay ghi chú nào trước hoặc sau JSON.
Dùng dấu ngoặc kép """" cho mọi key và mọi giá trị chuỗi (không dùng nháy đơn '). Dấu """" nằm trong giá trị chuỗi phải escape thành """". Ký tự xuống dòng trong giá trị phải escape thành \n (không xuống dòng thật trong chuỗi JSON).
KHÔNG để dấu phẩy dư (trailing comma) trước }} hoặc ].
JSON LUÔN chứa ĐỦ tất cả các trường được yêu cầu, đúng tên, đúng thứ tự như cấu trúc đã cho — kể cả khi giá trị là null. KHÔNG thêm, KHÔNG bớt, KHÔNG đổi tên trường.

[VÍ DỤ MINH HỌA CÁCH SUY LUẬN]
(dữ liệu và tên trường dưới đây là GIẢ ĐỊNH, chỉ để minh họa cách suy luận tọa độ → JSON — KHÔNG phải danh sách trường cố định) OCR đầu vào (ví dụ):
[50, 120, 180, 140] Tên trạm biến áp:
[190, 120, 420, 140] TBA Mẫu Số 01
[600, 122, 750, 140] Trang 1/3
[50, 150, 180, 170] Mã thiết bị:
[190, 150, 350, 170] AB-0102-XYZ
[50, 200, 180, 220] Địa chỉ:
[190, 200, 480, 220] Số 10, đường Ví Dụ,
[190, 225, 480, 245] Phường Mẫu, Quận Mẫu

Giả sử trường cần trích xuất là: ten_tba, ma_thiet_bi, dia_chi, ngay_kiem_tra. JSON đúng cần trả về:
{{""ten_tba"": ""TBA Mẫu Số 01"", ""ma_thiet_bi"": ""AB-0102-XYZ"", ""dia_chi"": ""Số 10, đường Ví Dụ,\nPhường Mẫu, Quận Mẫu"", ""ngay_kiem_tra"": null}}
Vì sao: ""TBA Mẫu Số 01"" được lấy vì cùng hàng, ngay bên phải nhãn ""Tên trạm biến áp:""; ""Trang 1/3"" bị bỏ qua vì nằm ở vùng x khác (chữ nhiễu góc phải, không phải giá trị của nhãn nào); ""AB-0102-XYZ"" giữ nguyên dù trông lạ vì là mã định danh; ""Địa chỉ"" được ghép 2 dòng bằng \n; ""ngay_kiem_tra"" không xuất hiện trong OCR nên trả về null thay vì đoán.

[TỰ KIỂM TRA TRƯỚC KHI TRẢ LỜI] (thực hiện trong nội bộ, KHÔNG hiển thị ra câu trả lời)
Trước khi xuất câu trả lời cuối cùng, tự rà soát: (a) mỗi giá trị đã điền có thực sự xuất hiện nguyên văn trong OCR không, hay là suy diễn? (b) có khối chữ nào bị lấy nhầm từ cột/bảng bên cạnh không? (c) ngoặc kép và ngoặc nhọn đã đóng đủ chưa, có dấu phẩy dư không? Sửa lại nếu phát hiện sai sót — phần rà soát này KHÔNG được xuất hiện trong câu trả lời.
{(taskMsg.Form != null ? taskMsg.ExtractPrompt : string.Empty)}";

                            string userPrompt = "";
                            if (taskMsg.Form != null && taskMsg.Form.Fields != null)
                            {
                                var fieldsList = new List<string>();
                                foreach (var field in taskMsg.Form.Fields)
                                {
                                    fieldsList.Add($"- {field.FieldName}: {field.Description}");
                                }
                                string fieldsStr = string.Join("\n", fieldsList);
                                userPrompt = $@"[CÁC TRƯỜNG CẦN TRÍCH XUẤT]:
{fieldsStr}";
                            }
                            else
                            {
                                userPrompt = "[CÁC TRƯỜNG CẦN TRÍCH XUẤT]:\nTrích xuất tất cả các thông tin quan trọng có trong văn bản.";
                            }

                            // 5. Gửi từng trang lên LLM
                            var tasks = new List<Task<PageResult>>();
                            List<PageResult> finalResults = new List<PageResult>();

                            using var semaphore = new SemaphoreSlim(1, 1);
                            int completedPages = 0;

                            for (int i = 0; i < totalPages; i++)
                            {
                                string pageText = pageTexts[i];
                                int pageNum = i + 1;

                                if (!pagesToExtract.Contains(pageNum))
                                {
                                    _logger.LogInformation(
                                        "Trang {Page} ngoài phạm vi bóc tách ({Scope}). Bỏ qua.",
                                        pageNum, ExtractionScopes.Describe(taskMsg.ExtractionScope));
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(pageText))
                                {
                                    _logger.LogInformation("Trang {Page} không có text. Bỏ qua.", pageNum);
                                    continue;
                                }

                                // Local function to handle each page asynchronously
                                async Task<PageResult> ProcessPageAsync()
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

                                        PageResult? pageResult = null;
                                        var swTotal = Stopwatch.StartNew();

                                        for (int attempt = 0; attempt <= maxRetries; attempt++)
                                        {
                                            if (attempt > 0)
                                            {
                                                _logger.LogWarning("Retry lần {Attempt}/{MaxRetries} cho trang {Page}.", attempt, maxRetries, pageNum);
                                                await Task.Delay(retryDelayMs * attempt, stoppingToken);
                                            }

                                            string? extractedJson = null;
                                            var sw = Stopwatch.StartNew();

                                            try
                                            {
                                                var payload = new
                                                {
                                                    messages = new object[]
                                                    {
                                                        new { role = "system", content = systemPrompt },
                                                        new { role = "user", content = $"{userPrompt}\n[VĂN BẢN OCR]:\n{pageText}" }
                                                    },
                                                    temperature = llmTemperature,
                                                    max_tokens = maxTokens,
                                                    response_format = new { type = "json_object" }
                                                };
                                                var content = new StringContent(
                                                    JsonSerializer.Serialize(payload, OcrPageContentHelper.Utf8JsonOptions),
                                                    Encoding.UTF8,
                                                    "application/json");

                                                var response = await httpClient.PostAsync("/v1/chat/completions", content, stoppingToken);
                                                response.EnsureSuccessStatusCode();
                                                var resultStr = await response.Content.ReadAsStringAsync(stoppingToken);
                                                sw.Stop();

                                                var jsonNode = JsonNode.Parse(resultStr);
                                                extractedJson = jsonNode?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
                                            }
                                            catch (Exception httpEx)
                                            {
                                                _logger.LogWarning(httpEx, "Lỗi gọi LLM/parse envelope, trang {Page}, attempt {Attempt}.", pageNum, attempt);
                                                if (attempt < maxRetries) continue;
                                                pageResult = new PageResult(pageNum, null, "", httpEx.Message, "error");
                                                break;
                                            }

                                            if (string.IsNullOrEmpty(extractedJson))
                                            {
                                                _logger.LogWarning(
                                                    "[DIAGNOSTIC] LLM response content rỗng cho trang {Page} (attempt {Attempt}).",
                                                    pageNum, attempt);
                                                if (attempt < maxRetries) continue;
                                                pageResult = new PageResult(pageNum, null, "", null, "empty");
                                                break;
                                            }

                                            extractedJson = OcrPageContentHelper.StripMarkdownCodeFence(extractedJson);

                                            try
                                            {
                                                var parsedJson = JsonNode.Parse(extractedJson);
                                                _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành Trích xuất sau {ElapsedMs} ms.", pageNum, sw.ElapsedMilliseconds);
                                                pageResult = new PageResult(pageNum, parsedJson, null, null, "direct");
                                                break;
                                            }
                                            catch
                                            {
                                                _logger.LogWarning("JSON parse lỗi trang {Page} (attempt {Attempt}): {Json}",
                                                    pageNum, attempt, extractedJson?.Length > 300 ? extractedJson[..300] + "..." : extractedJson);

                                                if (attempt < maxRetries) continue;

                                                try
                                                {
                                                    var dict = new Dictionary<string, object?>();
                                                    bool rescued = false;
                                                    if (taskMsg.Form?.Fields != null)
                                                    {
                                                        var keys = taskMsg.Form.Fields.Select(f => f.FieldName).ToList();
                                                        for (int k = 0; k < keys.Count; k++)
                                                        {
                                                            string key = keys[k];
                                                            string? nextKey = k < keys.Count - 1 ? keys[k + 1] : null;

                                                            var match = Regex.Match(extractedJson, $"\"{Regex.Escape(key)}\"\\s*:\\s*");
                                                            if (match.Success)
                                                            {
                                                                int startValIdx = match.Index + match.Length;
                                                                int endValIdx = extractedJson.Length;

                                                                if (nextKey != null)
                                                                {
                                                                    var nextMatch = Regex.Match(extractedJson, $"\"{Regex.Escape(nextKey)}\"\\s*:\\s*");
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
                                                        pageResult = new PageResult(pageNum, parsedJson, null, null, "rescued");
                                                        break;
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.LogWarning(ex, "Lỗi khi chạy cứu hộ JSON cho trang {Page}", pageNum);
                                                }

                                                _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành Trích xuất (Raw Text) sau {ElapsedMs} ms.", pageNum, swTotal.ElapsedMilliseconds);
                                                pageResult = new PageResult(pageNum, null, extractedJson, null, "raw_text");
                                                break;
                                            }
                                        }

                                        return pageResult ?? new PageResult(pageNum, null, "", null, "empty");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Lỗi khi gọi llm_server cho trang {Page}.", pageNum);
                                        return new PageResult(pageNum, null, null, ex.Message, "error");
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                        int currentCompleted = Interlocked.Increment(ref completedPages);
                                        // Best-effort — nằm trong finally, throw ở đây sẽ làm hỏng cả
                                        // Task.WhenAll bên dưới; mất 1 lần publish tiến trình vô hại.
                                        var progressMsg = new
                                        {
                                            FileId = taskMsg.FileId,
                                            Action = "extraction.process.progress",
                                            CurrentPage = currentCompleted,
                                            TotalPages = totalPages,
                                            Progress = (int)Math.Round((double)currentCompleted / totalPages * 100)
                                        };
                                        await publisher.TryPublishMessageAsync(progressMsg, "digitization.topic", "extraction.process.progress");
                                    }
                                }

                                tasks.Add(ProcessPageAsync());
                            }

                            // Đợi tất cả các task hoàn thành
                            var resultsArray = await Task.WhenAll(tasks);
                            finalResults = resultsArray.Where(r => r != null).OrderBy(r => r.Page).ToList();

                            // Fix 1: Merge thông minh — gộp kết quả tất cả trang thành 1 JSON object
                            // Ưu tiên giá trị non-null đầu tiên tìm thấy cho mỗi trường
                            var mergedResult = new JsonObject();
                            foreach (var result in finalResults)
                            {
                                try
                                {
                                    if (result.Data is JsonObject pageData)
                                    {
                                        foreach (var kvp in pageData)
                                        {
                                            string fieldName = kvp.Key;
                                            var fieldValue = kvp.Value;

                                            // Chỉ ghi đè nếu trường chưa có giá trị hoặc giá trị hiện tại là null
                                            if (!mergedResult.ContainsKey(fieldName))
                                            {
                                                mergedResult[fieldName] = fieldValue != null ? JsonNode.Parse(fieldValue.ToJsonString()) : null;
                                            }
                                            else if (mergedResult[fieldName] == null && fieldValue != null)
                                            {
                                                mergedResult[fieldName] = JsonNode.Parse(fieldValue.ToJsonString());
                                            }
                                            else if (mergedResult[fieldName] != null && fieldValue != null)
                                            {
                                                var oldVal = mergedResult[fieldName]!.ToJsonString();
                                                var newVal = fieldValue.ToJsonString();
                                                if (oldVal != newVal)
                                                {
                                                    _logger.LogWarning("Trường {Field} có giá trị khác nhau giữa các trang. Cũ: {Old}, Mới (bị bỏ qua): {New}", fieldName, oldVal, newVal);
                                                }
                                            }
                                        }
                                    }
                                    // Xử lý data_text: nếu trang không có "data" nhưng có "data_text" chứa JSON hợp lệ
                                    else if (!string.IsNullOrWhiteSpace(result.DataText))
                                    {
                                        try
                                        {
                                            string cleaned = OcrPageContentHelper.StripMarkdownCodeFence(result.DataText);
                                            var parsedDataText = JsonNode.Parse(cleaned);
                                            if (parsedDataText is JsonObject textPageData)
                                            {
                                                foreach (var kvp in textPageData)
                                                {
                                                    if (!mergedResult.ContainsKey(kvp.Key))
                                                        mergedResult[kvp.Key] = kvp.Value != null ? JsonNode.Parse(kvp.Value.ToJsonString()) : null;
                                                    else if (mergedResult[kvp.Key] == null && kvp.Value != null)
                                                        mergedResult[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                                                }
                                                _logger.LogInformation("Đã merge thành công data_text từ trang {Page}.", result.Page);
                                            }
                                        }
                                        catch (Exception dtEx)
                                        {
                                            _logger.LogWarning("Không parse được data_text trang {Page}: {Error}", result.Page, dtEx.Message);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Không thể merge kết quả trang {Page}: {Error}", result.Page, ex.Message);
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
                            string fileSuffix = taskMsg.EquipmentId.HasValue ? $"_eq_{taskMsg.EquipmentId.Value}" : "";
                            var resultFileName = string.IsNullOrEmpty(directory) 
                                ? $"extraction_result_{taskMsg.FileId}{fileSuffix}.json" 
                                : $"{directory}/extraction_result_{taskMsg.FileId}{fileSuffix}.json";
                            await minioService.UploadFileAsync(taskMsg.BucketName, resultFileName, resultStream, "application/json");
                            _logger.LogInformation("Đã lưu kết quả gộp vào MinIO: {FileName} tại bucket {BucketName}", resultFileName, taskMsg.BucketName);

                            bool allFailed = true;
                            if (finalResults.Count > 0)
                            {
                                foreach (var r in finalResults)
                                {
                                    if (string.IsNullOrEmpty(r.Error))
                                    {
                                        allFailed = false;
                                        break;
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
                                Status = status,
                                EquipmentId = taskMsg.EquipmentId
                            };
                            // Tín hiệu hoàn thành cuối cùng của tài liệu — thử lại có backoff; nếu vẫn thất
                            // bại, chấp nhận (đã cố hết sức) và để watchdog đóng job thay vì làm lại toàn bộ
                            // (làm lại tốn kém hơn OCR nhiều vì phải gọi LLM lại từ đầu).
                            await publisher.TryPublishMessageAsync(completedMsg, "digitization.topic", "extraction.process.completed",
                                maxAttempts: 3, initialDelay: TimeSpan.FromSeconds(2));
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
                        try
                        {
                            await HandleTaskFailureAsync(ea, body, messageText, ex, stoppingToken);
                        }
                        catch (Exception fatalEx)
                        {
                            // Lưới bảo vệ cuối cùng — tuyệt đối không để exception thoát khỏi callback này,
                            // vì làm vậy sẽ khiến consumer ngừng nhận message mới vĩnh viễn (đã xảy ra thực tế).
                            _logger.LogCritical(fatalEx,
                                "HandleTaskFailureAsync thất bại nghiêm trọng — thử nack (requeue) message gốc để tránh mất dữ liệu/treo consumer.");
                            try
                            {
                                await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                            }
                            catch (Exception nackEx)
                            {
                                _logger.LogCritical(nackEx,
                                    "Nack cũng thất bại — message có thể bị kẹt ở trạng thái unacked, cần kiểm tra RabbitMQ management UI thủ công.");
                            }
                        }
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

        private const int MaxRetries = 3;

        /// <summary>
        /// Trước đây lỗi bị nack không requeue (mất message âm thầm, không DLQ, không báo trạng thái).
        /// Nay thử lại tối đa <see cref="MaxRetries"/> lần (đếm qua header "x-retry-count"), vượt quá thì
        /// đẩy sang hàng đợi lỗi (DLQ) và báo ngay cho EquipmentService để đánh dấu Failed.
        /// </summary>
        private async Task HandleTaskFailureAsync(
            BasicDeliverEventArgs ea,
            byte[] body,
            string messageText,
            Exception ex,
            CancellationToken cancellationToken)
        {
            var retryCount = GetRetryCount(ea.BasicProperties?.Headers) + 1;
            var isFinalAttempt = retryCount > MaxRetries;
            var targetRoutingKey = isFinalAttempt
                ? DigitizationTopicTopology.ExtractionTaskDeadLetterRoutingKey
                : DigitizationTopicTopology.ExtractionTaskRoutingKey;

            // Publish/republish TRƯỚC, chỉ ack message gốc SAU KHI thành công — nếu đảo ngược thứ tự,
            // ack thất bại-republish sẽ làm mất message vĩnh viễn (đã ack nhưng bản republish chưa vào queue).
            var republished = await RepublishRawAsync(body, targetRoutingKey, retryCount, cancellationToken);
            if (!republished)
            {
                _logger.LogError(
                    "Không thể publish lại/DLQ message Extraction (routing key {RoutingKey}) — trả message về queue gốc (nack requeue) để không mất dữ liệu.",
                    targetRoutingKey);
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken);
                return;
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);

            if (!isFinalAttempt)
            {
                _logger.LogWarning("Extraction task lỗi, thử lại lần {RetryCount}/{MaxRetries}.", retryCount, MaxRetries);
                return;
            }

            _logger.LogError(
                "Extraction task vượt quá {MaxRetries} lần thử — đã chuyển sang hàng đợi lỗi {DlqQueue}.",
                MaxRetries, DigitizationTopicTopology.ExtractionTaskDeadLetterQueue);

            var fileId = TryExtractFileId(messageText);
            if (fileId.HasValue)
            {
                using var scope = _serviceProvider.CreateScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
                var failedMsg = new
                {
                    FileId = fileId.Value,
                    Action = "extraction.process.failed",
                    CurrentPage = 0,
                    TotalPages = 0,
                    Progress = 0,
                    ErrorMessage = ex.Message
                };
                // Thử lại có backoff — nếu vẫn thất bại sau cùng, watchdog (dựa trên ModifiedDate, độc
                // lập RabbitMQ) sẽ tự đóng job này sau tối đa 30 phút, nên không cần throw/chặn ở đây.
                await publisher.TryPublishMessageAsync(failedMsg, "digitization.topic", "extraction.process.progress",
                    maxAttempts: 3, initialDelay: TimeSpan.FromSeconds(2));
            }
        }

        private async Task<bool> RepublishRawAsync(byte[] body, string routingKey, int retryCount, CancellationToken cancellationToken)
        {
            try
            {
                var props = new BasicProperties
                {
                    Headers = new Dictionary<string, object?> { ["x-retry-count"] = retryCount }
                };
                await _channel!.BasicPublishAsync(
                    exchange: "digitization.topic",
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RepublishRawAsync thất bại cho routing key {RoutingKey}.", routingKey);
                return false;
            }
        }

        private static int GetRetryCount(IDictionary<string, object?>? headers)
        {
            if (headers == null || !headers.TryGetValue("x-retry-count", out var raw) || raw == null)
                return 0;

            try
            {
                return Convert.ToInt32(raw is byte[] bytes ? Encoding.UTF8.GetString(bytes) : raw);
            }
            catch
            {
                return 0;
            }
        }

        private static Guid? TryExtractFileId(string messageText)
        {
            try
            {
                var task = JsonSerializer.Deserialize<ExtractionTaskMessage>(messageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return task?.FileId;
            }
            catch
            {
                return null;
            }
        }
    }
}
