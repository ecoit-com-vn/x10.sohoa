// e:/ecoit/sohoax10/sohoa.backend/Microservices/EvnHanoi.DigitizationService/Workers/OcrWorker.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json.Serialization;
using PdfDocument = PdfSharpCore.Pdf.PdfDocument;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.DigitizationService.Services.OcrModule;
using EvnHanoi.DigitizationService.Helpers;
using EvnHanoi.Infrastructure.Messaging;
using PdfSharpCore.Pdf;

namespace EvnHanoi.DigitizationService.Workers
{
    /// <summary>
    /// DTO nhận kết quả OCR [{text, box, confidence}] từ ocr_vl_server.
    /// </summary>
    public class TextBoxResponse
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("box")]
        public List<float> Box { get; set; } = new();

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }
    }

    /// <summary>
    /// DTO parse kết quả sửa chính tả từ LLM: [{index, text}]
    /// </summary>
    public class CorrectedTextItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    /// <summary>
    /// Worker tiêu thụ message từ RabbitMQ queue "ocr_task_queue".
    /// 
    /// Luồng xử lý:
    ///   1. Tải PDF từ MinIO
    ///   2. Render từng trang PDF → JPEG (150 DPI)
    ///   3. Gửi 1 POST /ocr_page đến ocr_vl_server → nhận [{text, box}]
    ///   4. Vẽ text ẩn (invisible layer) lên ảnh PDF tạo PDF 2 lớp
    ///   5. Upload PDF 2 lớp lên MinIO, publish ExtractionTask
    /// </summary>
    public class OcrWorker : BackgroundService
    {
        private readonly ILogger<OcrWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnection _connection;
        private readonly OcrLane _lane;
        private IChannel? _channel;
        private readonly string _ocrVlServerUrl;
        private readonly string _llmServerUrl;

        public OcrWorker(
            ILogger<OcrWorker> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            IConnection connection,
            OcrLane lane)
        {
            _lane = lane;
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _connection = connection;
            _ocrVlServerUrl = _configuration["AIModelServers:OcrVlServerUrl"] ?? "http://ocr3.ecoit.asia";
            _llmServerUrl = _configuration["AIModelServers:LlmServerUrl"] ?? "http://localhost:8080";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Số message xử lý đồng thời của channel này = prefetch của luồng (theo từng channel, không dùng chung cho cả kết nối —
                // nếu không ExtractionWorker trên cùng kết nối cũng bị đổi mức song song khi chỉnh số chỗ của OCR).
                _channel = await _connection.CreateChannelAsync(
                    new CreateChannelOptions(
                        publisherConfirmationsEnabled: false,
                        publisherConfirmationTrackingEnabled: false,
                        consumerDispatchConcurrency: _lane.PrefetchCount),
                    cancellationToken: stoppingToken);

                var exchangeName = "digitization.topic";
                // Mỗi luồng (nhỏ/lớn) là một instance OcrWorker riêng, có channel + queue + prefetch riêng.
                var queueName = _lane.QueueName;
                var routingKey = _lane.RoutingKey;

                await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey, cancellationToken: stoppingToken);

                // Giới hạn số message chưa ack của luồng này bằng đúng prefetch của luồng (= consumerDispatchConcurrency của channel ở trên)
                // — để RabbitMQ không dồn quá nhiều message cho worker. Mỗi tài liệu OCR chiếm GPU rất lâu (tài liệu thật 96–744 vùng
                // chữ/trang) nên giữ nhiều message unacked chỉ làm chúng chờ tới lúc vượt timeout, không giúp nhanh hơn.
                // Số tài liệu song song: Ocr:PrefetchCount (nhỏ) / Ocr:LargePrefetchCount (lớn) — xem OcrWorkerSettings.
                var prefetchCount = _lane.PrefetchCount;
                _logger.LogInformation("OcrWorker luồng {Lane}: queue {Queue}, xử lý song song tối đa {Prefetch} tài liệu.", _lane.Name, queueName, prefetchCount);
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: prefetchCount, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);
                    //_logger.LogInformation("Nhận yêu cầu OCR: {Message}", messageText);

                    // Chế độ xả: luồng lớn bị tắt (Ocr:LargeLaneEnabled=false) → trả message về queue chính cho luồng nhỏ xử lý, KHÔNG OCR.
                    // Dùng trước khi rollback image (bản cũ không đọc queue lớn) hoặc khi luồng lớn có sự cố.
                    if (_lane.IsLarge && !OcrWorkerSettings.IsLargeLaneEnabled(_configuration))
                    {
                        await DrainToStandardLaneAsync(ea, body, stoppingToken);
                        return;
                    }

                    var largeJob = false;
                    try
                    {
                        var taskMsg = JsonSerializer.Deserialize<OcrTaskMessage>(messageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (taskMsg != null)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var repository = scope.ServiceProvider.GetRequiredService<IFileAttachmentRepository>();
                            var minioService = scope.ServiceProvider.GetRequiredService<IMinioStorageService>();
                            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                            var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
                            var pdfBuilder = scope.ServiceProvider.GetRequiredService<ISearchablePdfBuilder>();
                            // Dùng chung cho việc cập nhật % tiến trình mỗi trang (bên dưới) và nhánh
                            // materialize khi hoàn tất (trong PublishExtractionAndAckAsync) — chỉ áp dụng
                            // cho luồng "Quản lý dữ liệu huấn luyện AI-OCR" (có OcrModuleJobId).
                            var ocrModuleRepo = scope.ServiceProvider.GetRequiredService<IOcrModuleRepository>();

                            // Bàn giao sang ExtractionWorker — dùng chung cho luồng bình thường và
                            // luồng bỏ qua khi phát hiện file đã là PDF 2 lớp (idempotency, xem dưới).
                            async Task PublishExtractionAndAckAsync(string extractionFilePath)
                            {
                                // Thao tác "OCR" độc lập (ProcessOption = OcrOnly): dừng lại ngay sau khi
                                // OCR xong, không tự động chuyển sang bóc tách. Message tiến trình
                                // "ocr.process.progress" ở trang cuối (Progress=100) đã cập nhật Status =
                                // "OcrCompleted" cho FE — người dùng tự bấm "Bóc tách" riêng khi cần.
                                if (taskMsg.ProcessOption == "OcrOnly")
                                {
                                    _logger.LogInformation(
                                        "Task yêu cầu OcrOnly, dừng lại sau OCR cho file {FilePath}, không chuyển sang Extraction.",
                                        taskMsg.FilePath);
                                    await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                                    return;
                                }

                                // Luồng "Quản lý dữ liệu huấn luyện AI-OCR" (upload PDF độc lập từ màn hình
                                // quản trị, không gắn Dossier/Equipment): không cần bóc tách LLM, chỉ nạp
                                // thẳng kết quả OCR vừa ghi ra MinIO vào OCR_MODULE_REGION rồi đóng Job.
                                // Mọi luồng dossier/equipment hiện tại không set OcrModuleJobId nên rơi
                                // xuống nhánh publish extraction.process.task như cũ, không đổi hành vi.
                                if (!string.IsNullOrEmpty(taskMsg.OcrModuleJobId))
                                {
                                    var ocrJsonMaterializer = scope.ServiceProvider.GetRequiredService<IOcrJsonMaterializer>();

                                    try
                                    {
                                        var job = await ocrModuleRepo.GetJobByIdAsync(taskMsg.OcrModuleJobId);
                                        var totalPages = job?.TotalPages ?? 0;
                                        var regions = await ocrJsonMaterializer.MaterializeAsync(
                                            taskMsg.OcrModuleJobId, taskMsg.BucketName, extractionFilePath, totalPages);
                                        await ocrModuleRepo.InsertRegionsAsync(regions);

                                        var actualPages = regions.Count > 0 ? regions.Max(r => r.PageNumber) : totalPages;
                                        await ocrModuleRepo.UpdateJobStateAsync(taskMsg.OcrModuleJobId, "Ready", actualPages, null);
                                        _logger.LogInformation(
                                            "Job huấn luyện AI-OCR {JobId} đã nạp {RegionCount} vùng, chuyển trạng thái Ready.",
                                            taskMsg.OcrModuleJobId, regions.Count);
                                    }
                                    catch (Exception materializeEx)
                                    {
                                        _logger.LogError(materializeEx,
                                            "Lỗi nạp OCR_MODULE_REGION cho Job huấn luyện AI-OCR {JobId}.", taskMsg.OcrModuleJobId);
                                        try
                                        {
                                            await ocrModuleRepo.UpdateJobStateAsync(taskMsg.OcrModuleJobId, "Failed", 0, materializeEx.Message);
                                        }
                                        catch (Exception updateFailedEx)
                                        {
                                            _logger.LogError(updateFailedEx,
                                                "Không thể cập nhật trạng thái Failed cho Job huấn luyện AI-OCR {JobId}.", taskMsg.OcrModuleJobId);
                                        }
                                    }

                                    await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                                    return;
                                }

                                var extractionTask = new ExtractionTaskMessage
                                {
                                    FileId = taskMsg.FileId,
                                    FilePath = extractionFilePath,
                                    BucketName = taskMsg.BucketName,
                                    ExtractPrompt = taskMsg.ExtractPrompt,
                                    Form = taskMsg.Form,
                                    FormSchemaJson = taskMsg.FormSchemaJson,
                                    EquipmentId = taskMsg.EquipmentId,
                                    // Chuyển tiếp phạm vi trang người dùng chọn lúc upload — OCR ở trên
                                    // đã chạy đủ trang, chỉ bước bóc tách mới bị giới hạn.
                                    ExtractionScope = taskMsg.ExtractionScope
                                };
                                // Tín hiệu bàn giao sang ExtractionWorker — mất là job "xong OCR nhưng
                                // không bao giờ vào Extraction" âm thầm, nên thử lại có backoff trước
                                // khi chấp nhận thất bại.
                                var handoffOk = await publisher.TryPublishMessageAsync(
                                    extractionTask, "digitization.topic", "extraction.process.task",
                                    maxAttempts: 3, initialDelay: TimeSpan.FromSeconds(2));
                                if (!handoffOk)
                                {
                                    throw new InvalidOperationException(
                                        $"Không thể publish extraction.process.task cho file {taskMsg.FileId} sau 3 lần thử.");
                                }

                                await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                            }

                            if (taskMsg.ProcessOption == "ExtractOnly")
                            {
                                _logger.LogInformation("Task yêu cầu ExtractOnly, bỏ qua OCR, chuyển thẳng sang Extraction.");
                                var extractMsg = new ExtractionTaskMessage
                                {
                                    FileId = taskMsg.FileId,
                                    FilePath = taskMsg.FilePath,
                                    BucketName = taskMsg.BucketName,
                                    ExtractPrompt = taskMsg.ExtractPrompt,
                                    Form = taskMsg.Form,
                                    FormSchemaJson = taskMsg.FormSchemaJson,
                                    EquipmentId = taskMsg.EquipmentId,
                                    ExtractionScope = taskMsg.ExtractionScope
                                };
                                await publisher.PublishMessageAsync(extractMsg, "digitization.topic", "extraction.process.task");
                                await _channel.BasicAckAsync(ea.DeliveryTag, false);
                                return;
                            }
                            else
                            {
                                _logger.LogInformation("Task yêu cầu OCR + Extraction.");
                            }

                            // Tách luồng THEO GỢI Ý của publisher (số trang/dung lượng trong DB) — chuyển sang luồng lớn TRƯỚC khi tải file nên không tốn băng thông/RAM.
                            // Gợi ý có thể thiếu hoặc lệch → vẫn có kiểm tra lại sau khi tải (bên dưới).
                            if (!_lane.IsLarge && OcrWorkerSettings.IsLargeLaneEnabled(_configuration)
                                && (taskMsg.PageCount.HasValue || taskMsg.FileSizeBytes.HasValue)
                                && OcrWorkerSettings.IsLargeJob(_configuration, taskMsg.PageCount ?? 0, taskMsg.FileSizeBytes ?? 0))
                            {
                                if (await TryRedirectToLargeLaneAsync(
                                        ea, body, taskMsg.FileId, taskMsg.PageCount ?? 0, taskMsg.FileSizeBytes ?? 0, "gợi ý của publisher, trước khi tải file", stoppingToken))
                                    return;
                            }

                            // 1. Tải file PDF từ MinIO
                            _logger.LogInformation("Tải file {FilePath} từ bucket {BucketName}", taskMsg.FilePath, taskMsg.BucketName);
                            byte[] pdfBytes = await DownloadPdfBytesAsync(minioService, taskMsg.BucketName, taskMsg.FilePath, stoppingToken);
                            LogMemory("sau khi tải file", taskMsg.FileId, 0, pdfBytes.Length);

                            // Ngưỡng an toàn (Ocr:MaxFileSizeMB, mặc định 250): file quá lớn báo lỗi rõ ràng ngay, không thử lại (thử lại chỉ làm hết RAM lặp lại).
                            var maxFileMb = _configuration.GetValue("Ocr:MaxFileSizeMB", 250);
                            if (maxFileMb > 0 && pdfBytes.LongLength > maxFileMb * 1024L * 1024L)
                            {
                                throw new NonRetryableOcrException(
                                    $"File quá lớn ({pdfBytes.LongLength / 1048576} MB > giới hạn {maxFileMb} MB) — hãy tách thành các file nhỏ hơn rồi tải lại.");
                            }

                            // Lớp phòng thủ: ảnh đáng lẽ đã được chuyển thành PDF lúc upload. File không phải PDF
                            // (ảnh upload từ trước khi có tính năng, hoặc đường upload bỏ sót) → báo lỗi rõ ràng và
                            // KHÔNG retry (retry vô ích, chỉ chiếm GPU/queue) thay vì ném exception khó hiểu từ PDFtoImage.
                            if (!LooksLikePdf(pdfBytes))
                            {
                                throw new NonRetryableOcrException(
                                    $"File {taskMsg.FilePath} không phải PDF (có thể là file ảnh chưa được chuyển đổi) — OCR chỉ xử lý PDF.");
                            }

                            // Idempotency: nếu file đã được OcrWorker dựng PDF 2 lớp từ trước (marker
                            // /EvnOcrVersion trong Info) mà message này vẫn tới (retry, requeue thủ
                            // công...), KHÔNG OCR/vẽ lại — job trước đã ghi đè lên bản gốc trên MinIO,
                            // dựng lại lần nữa sẽ vẽ đè lớp text thứ hai lên lớp đã có.
                            if (pdfBuilder.IsAlreadySearchable(pdfBytes))
                            {
                                _logger.LogWarning(
                                    "File {FilePath} đã có marker PDF 2 lớp — bỏ qua OCR, chuyển thẳng sang Extraction.",
                                    taskMsg.FilePath);
                                await PublishExtractionAndAckAsync(taskMsg.FilePath);
                                return;
                            }

                            int pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
                            _logger.LogInformation("PDF có {PageCount} trang. Bắt đầu xử lý từng trang.", pageCount);

                            var maxPages = _configuration.GetValue("Ocr:MaxPages", 600);
                            if (maxPages > 0 && pageCount > maxPages)
                            {
                                throw new NonRetryableOcrException(
                                    $"Tài liệu có {pageCount} trang, vượt giới hạn {maxPages} trang — hãy tách thành các file nhỏ hơn rồi tải lại.");
                            }
                            // Đánh dấu job lớn để dọn bộ nhớ (nén vùng đối tượng lớn) ở cuối — xem khối finally bên dưới.
                            largeJob = pdfBytes.Length >= 20 * 1024 * 1024 || pageCount >= 60;

                            // Tách luồng (kiểm tra lại sau khi tải — gợi ý của publisher có thể thiếu/lệch): tài liệu lớn được chuyển sang luồng lớn ngay tại đây,
                            // trước khi tốn GPU, để không chiếm chỗ của tài liệu nhỏ. Cố ý KHÔNG đặt largeJob = true cho nhánh này: việc đó ép 2 lần GC nén
                            // toàn bộ làm khựng cả luồng nhỏ; mảng PDF vừa tải sẽ được GC thường thu hồi.
                            if (!_lane.IsLarge && OcrWorkerSettings.IsLargeLaneEnabled(_configuration)
                                && OcrWorkerSettings.IsLargeJob(_configuration, pageCount, pdfBytes.LongLength))
                            {
                                if (await TryRedirectToLargeLaneAsync(
                                        ea, body, taskMsg.FileId, pageCount, pdfBytes.LongLength, "kiểm tra sau khi tải file", stoppingToken))
                                    return;
                            }

                            using var pdfDocument = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);

                            // Timeout thực tế do AddStandardResilienceHandler kiểm soát (xem Program.cs, cấu hình qua AIModelServers:OcrPage*)
                            var httpClient = httpClientFactory.CreateClient("OcrPageClient");
                            httpClient.Timeout = Timeout.InfiniteTimeSpan;

                            var llmClient = httpClientFactory.CreateClient("LlmClient");
                            llmClient.BaseAddress = new Uri(_llmServerUrl);
                            llmClient.Timeout = TimeSpan.FromMinutes(5);

                            // Tạo PDF 2 lớp output
                            // PDF 2 lớp dựng THEO LÔ TRANG (Ocr:PdfChunkPages, mặc định 10; 0 = một PdfDocument như cũ): PdfSharpCore giữ ảnh ĐÃ GIẢI MÃ của mọi
                            // trang (khoảng 14 MB/trang A4, hàng chục MB/trang khổ lớn) cho tới khi Save → RAM tăng theo số trang. Mỗi lô đủ số trang được lưu ra file
                            // tạm rồi giải phóng ảnh, cuối cùng ghép các lô (chỉ nạp byte JPEG nén). Tài liệu ≤ 1 lô đi đúng đường cũ.
                            var pdfChunkPages = _configuration.GetValue("Ocr:PdfChunkPages", 10);
                            using var chunkFiles = new TempFileList();
                            var outPdfDoc = new PdfDocument();
                            int pagesInChunk = 0;
                            int totalBoxesDrawn = 0;

                            for (int i = 0; i < pageCount; i++)
                            {
                                _logger.LogInformation("Đang xử lý trang {Page}/{TotalPages}...", i + 1, pageCount);

                                // 2. Render trang PDF → JPEG (OcrModuleImageDpi.OcrSourceDpi — khớp với DPI file
                                // đã được nén lúc upload ở EquipmentService, xem EvnHanoi.DocumentProcessing;
                                // render lại cao hơn từ nguồn đã 150 DPI chỉ tốn CPU, không thêm chi tiết thật).
                                // Toạ độ box OCR trả về theo hệ pixel của DPI này — OcrJsonMaterializer sẽ quy
                                // đổi sang OcrModuleImageDpi.DisplayDpi trước khi lưu, để khớp ảnh hiển thị cho FE.
                                using var imgStream = new MemoryStream();
                                var renderOptions = new PDFtoImage.RenderOptions { Dpi = OcrModuleImageDpi.OcrSourceDpi, WithAnnotations = true };
                                PDFtoImage.Conversion.SaveJpeg(imgStream, pdfBytes, password: null, page: i, options: renderOptions);
                                imgStream.Position = 0;
                                byte[] pageImageBytes = imgStream.ToArray();

                                if (pageImageBytes.Length == 0)
                                {
                                    // Không có ảnh thì không thể vẽ trang PDF 2 lớp — throw để job
                                    // vào retry/DLQ thay vì âm thầm bỏ qua rồi vẫn ghi đè bản gốc.
                                    throw new InvalidOperationException(
                                        $"Render trang {i + 1}/{pageCount} ra JPEG rỗng — không thể tạo PDF 2 lớp cho file {taskMsg.FileId}.");
                                }

                                var (imgWidthPx, imgHeightPx) = pdfBuilder.GetImagePixelSize(pageImageBytes);

                                List<TextBoxResponse> ocrResults = new();
                                var sw = Stopwatch.StartNew();

                                try
                                {
                                    if (pageImageBytes.Length > 0)
                                    {
                                        // 3. Gửi ảnh toàn trang lên ocr_vl_server → nhận [{text, box, confidence}]
                                        using var multipart = new MultipartFormDataContent();
                                        var imageContent = new ByteArrayContent(pageImageBytes);
                                        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                                        multipart.Add(imageContent, "file", $"page_{i + 1}.jpg");

                                        string ocrPageUrl = $"{_ocrVlServerUrl.TrimEnd('/')}/ocr_page";
                                        _logger.LogInformation("Gửi trang {Page} ({ImageBytes} bytes) lên {Url}", i + 1, pageImageBytes.Length, ocrPageUrl);

                                        var response = await httpClient.PostAsync(ocrPageUrl, multipart, stoppingToken);

                                        if (response.IsSuccessStatusCode)
                                        {
                                            var respStr = await response.Content.ReadAsStringAsync(stoppingToken);
                                            ocrResults = OcrPageContentHelper.DeserializeOcrResponse(respStr);
                                            foreach (var box in ocrResults)
                                                box.Text = OcrPageContentHelper.NormalizeUtf8Text(box.Text);
                                            if (ocrResults.Count == 0)
                                            {
                                                var preview = respStr.Length > 300 ? respStr[..300] + "..." : respStr;
                                                _logger.LogWarning(
                                                    "ocr_vl_server trả về 0 box cho trang {Page}. Response: {Response}",
                                                    i + 1, preview);
                                            }
                                        }
                                        else
                                        {
                                            // LỖI HẠ TẦNG, KHÔNG PHẢI "trang không có chữ" — phải ném ra để job vào
                                            // retry/DLQ và EquipmentService đánh dấu Failed. Trước đây chỗ này chỉ ghi
                                            // warning rồi tiếp tục với 0 box, nên trang lỗi vẫn được ghi vào PDF "2 lớp"
                                            // mà không có lớp text nào — mất dữ liệu âm thầm, chỉ phát hiện được nếu
                                            // TOÀN BỘ tài liệu rỗng (chốt chặn totalBoxesDrawn == 0 phía dưới).
                                            var errorBody = await response.Content.ReadAsStringAsync(stoppingToken);
                                            throw new InvalidOperationException(
                                                $"ocr_vl_server trả về lỗi {(int)response.StatusCode} {response.StatusCode} cho trang {i + 1}/{pageCount} " +
                                                $"(FileId={taskMsg.FileId}). Body: {(errorBody.Length > 500 ? errorBody[..500] + "..." : errorBody)}");
                                        }
                                    }

                                    if (ocrResults.Count == 0)
                                    {
                                        var rawText = pdfDocument.GetPage(i + 1).Text?.Trim();
                                        if (!string.IsNullOrEmpty(rawText))
                                        {
                                            ocrResults.Add(OcrPageContentHelper.CreateFullPageBox(rawText, imgWidthPx, imgHeightPx));
                                            _logger.LogInformation(
                                                "Trang {Page}: dùng PdfPig fallback ({CharCount} ký tự).",
                                                i + 1, rawText.Length);
                                        }
                                    }

                                    sw.Stop();
                                    _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành OCR sau {ElapsedMs} ms. Nhận về {Count} box text.",
                                        i + 1, sw.ElapsedMilliseconds, ocrResults.Count);
                                }
                                catch (Exception ex)
                                {
                                    sw.Stop();
                                    // KHÔNG nuốt lỗi: timeout/mất kết nối/HTTP lỗi khi gọi ocr_vl_server là lỗi thật,
                                    // phải để nó nổi lên handler ngoài -> retry có backoff -> DLQ, và publish
                                    // ocr.process.failed để EquipmentService đánh dấu tài liệu Failed. Ném ngay tại
                                    // trang lỗi (fail fast) thay vì chạy tiếp các trang còn lại: dù sao job cũng sẽ
                                    // được retry lại từ đầu, chạy tiếp chỉ chiếm GPU vô ích.
                                    _logger.LogError(ex,
                                        "Lỗi khi gọi ocr_vl_server cho trang {Page}/{TotalPages} (FileId={FileId}) — huỷ job.",
                                        i + 1, pageCount, taskMsg.FileId);
                                    throw;
                                }

                                // 4. Tạo trang PDF 2 lớp: text ẩn (vẽ trước) + ảnh gốc (phủ lên sau) —
                                // xem SearchablePdfBuilder.AddPage cho lý do vẽ theo thứ tự này và vì
                                // sao tài nguyên ảnh không được dispose ngay trong vòng lặp này.
                                // Ảnh gửi OCR giữ nguyên chất lượng; ảnh NHÚNG vào PDF được nén lại (Ocr:PdfJpegQuality, mặc định 80) để các trang giữ trong RAM
                                // tới khi ghi xong nhẹ hơn nhiều (PDF kết quả cũng nhỏ hơn).
                                byte[] pdfPageImage = RecompressJpegForPdf(pageImageBytes, i + 1);
                                totalBoxesDrawn += pdfBuilder.AddPage(outPdfDoc, pdfPageImage, ocrResults);
                                pagesInChunk++;
                                if (pdfChunkPages > 0 && pagesInChunk >= pdfChunkPages && i + 1 < pageCount)
                                {
                                    SaveChunkAndRelease(outPdfDoc, pdfBuilder, chunkFiles);
                                    outPdfDoc.Dispose();
                                    outPdfDoc = new PdfDocument();
                                    pagesInChunk = 0;
                                }
                                if ((i + 1) % 25 == 0 || i + 1 == pageCount)
                                    LogMemory($"sau trang {i + 1}/{pageCount} (ảnh nhúng {pdfPageImage.Length / 1024} KB, ảnh OCR {pageImageBytes.Length / 1024} KB)", taskMsg.FileId, i + 1, pdfBytes.Length);

                                // 4b. Sinh file JSON gốc cho page
                                string baseFilePath = taskMsg.FilePath;
                                if (baseFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                {
                                    baseFilePath = baseFilePath.Substring(0, baseFilePath.Length - 4);
                                }
                                string jsonFileName = $"{baseFilePath}_page_{i + 1}.json";

                                string pageJson = JsonSerializer.Serialize(ocrResults, OcrPageContentHelper.OcrJsonOptions);
                                using (var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(pageJson)))
                                {
                                    await minioService.UploadFileAsync(taskMsg.BucketName, jsonFileName, jsonStream, "application/json");
                                }
                                _logger.LogInformation("Đã upload file JSON cho trang {Page}: {FileName} ({BoxCount} box)", i + 1, jsonFileName, ocrResults.Count);

                                // Báo cáo tiến trình per-page — best-effort: file JSON của trang đã lưu MinIO
                                // xong ở trên, mất 1 lần publish tiến trình vô hại (trang sau tự bù %), nên
                                // KHÔNG được phép làm dừng vòng lặp nếu RabbitMQ chập chờn thoáng qua.
                                var progressMsg = new
                                {
                                    FileId = taskMsg.FileId,
                                    Action = "ocr.process.progress",
                                    CurrentPage = i + 1,
                                    TotalPages = pageCount,
                                    Progress = (int)Math.Round((double)(i + 1) / pageCount * 100)
                                };
                                await publisher.TryPublishMessageAsync(progressMsg, "digitization.topic", "ocr.process.progress");

                                // Job huấn luyện AI-OCR (không dossier/equipment): cập nhật % ngay trong
                                // OCR_MODULE_JOB — best-effort, không được chặn vòng lặp OCR nếu lỗi tạm thời.
                                if (!string.IsNullOrEmpty(taskMsg.OcrModuleJobId))
                                {
                                    try
                                    {
                                        await ocrModuleRepo.UpdateJobProgressAsync(taskMsg.OcrModuleJobId, i + 1, pageCount);
                                    }
                                    catch (Exception progressEx)
                                    {
                                        _logger.LogWarning(progressEx,
                                            "Không cập nhật được % tiến trình cho Job huấn luyện AI-OCR {JobId} (trang {Page}).",
                                            taskMsg.OcrModuleJobId, i + 1);
                                    }
                                }
                            }

                            // Chốt chặn: nếu KHÔNG vẽ được bất kỳ text nào trên toàn tài liệu (OCR
                            // server rỗng + PdfPig fallback cũng rỗng ở mọi trang), không được ghi đè
                            // bản gốc trên MinIO bằng một PDF "2 lớp" nhưng thực chất không có lớp
                            // text nào — throw để job vào retry/DLQ, giữ nguyên bản gốc.
                            if (totalBoxesDrawn == 0)
                            {
                                throw new InvalidOperationException(
                                    $"Không vẽ được text trên bất kỳ trang nào trong {pageCount} trang — huỷ tạo PDF 2 lớp để không ghi đè mất bản gốc trên MinIO (FileId={taskMsg.FileId}).");
                            }
                            if (chunkFiles.Paths.Count > 0)
                            {
                                // Lô cuối (chưa đủ số trang) cũng lưu ra file rồi ghép tất cả theo thứ tự trang.
                                SaveChunkAndRelease(outPdfDoc, pdfBuilder, chunkFiles);
                                outPdfDoc.Dispose();
                                outPdfDoc = MergeChunks(chunkFiles);
                                chunkFiles.Dispose(); // bản ghép đã nằm trong bộ nhớ → xoá file lô ngay để file tạm không gấp đôi dung lượng đĩa
                                LogMemory($"sau khi ghép {chunkFiles.Paths.Count} lô", taskMsg.FileId, pageCount, pdfBytes.Length);
                            }
                            pdfBuilder.MarkAsSearchable(outPdfDoc);

                            // 5. Upload PDF 2 lớp lên MinIO (ghi đè file gốc)
                            string outFileName = taskMsg.FilePath;
                            // Ghi ra FILE TẠM (tự xoá khi đóng) thay vì MemoryStream: bộ đệm Save tự nhân đôi dung lượng là đỉnh RAM lớn nhất của job. Không tạo
                            // được file tạm (thư mục chỉ đọc/đầy) thì quay về MemoryStream như cũ.
                            using var finalPdfStream = CreateTempOutputStream(taskMsg.FileId);
                            LogMemory("trước khi ghi PDF 2 lớp", taskMsg.FileId, pageCount, pdfBytes.Length);
                            outPdfDoc.Save(finalPdfStream, false);
                            finalPdfStream.Position = 0;
                            LogMemory($"sau khi ghi PDF 2 lớp ({finalPdfStream.Length / 1048576} MB, {(finalPdfStream is FileStream ? "file tạm" : "bộ nhớ")})", taskMsg.FileId, pageCount, pdfBytes.Length);

                            _logger.LogInformation("Đang upload (ghi đè) PDF 2 lớp {FileName} lên MinIO", outFileName);
                            await minioService.UploadFileAsync(taskMsg.BucketName, outFileName, finalPdfStream, "application/pdf");
                            outPdfDoc.Dispose(); // không còn `using var` (outPdfDoc được thay theo lô) — giải phóng ngay sau khi upload

                            // 6. Publish ExtractionTaskMessage → ExtractionWorker
                            await PublishExtractionAndAckAsync(outFileName);
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
                    finally
                    {
                        // Job lớn (file ≥ 20 MB hoặc ≥ 60 trang) để lại nhiều mảng lớn ở vùng LOH: nén + thu gom ngay để trả bộ nhớ trước khi nhận job kế tiếp.
                        if (largeJob) ReleaseLargeObjectMemory();
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

        /// <summary>Tải PDF về đúng 1 bản byte[]: MinioStorageService trả MemoryStream có mảng đúng cỡ nên dùng thẳng mảng đó (không sao chép);
        /// các trường hợp khác đọc vào mảng đúng cỡ (hoặc MemoryStream nếu không biết kích thước).</summary>
        private static async Task<byte[]> DownloadPdfBytesAsync(IMinioStorageService minioService, string bucket, string path, CancellationToken cancellationToken)
        {
            using var fileStream = await minioService.DownloadFileAsync(bucket, path);
            if (fileStream is MemoryStream ms && ms.TryGetBuffer(out var segment)
                && segment.Array != null && segment.Offset == 0 && segment.Array.Length == ms.Length)
            {
                return segment.Array;
            }
            if (fileStream.CanSeek && fileStream.Length > 0)
            {
                var exact = new byte[fileStream.Length];
                await fileStream.ReadExactlyAsync(exact, cancellationToken);
                return exact;
            }
            using var copy = new MemoryStream();
            await fileStream.CopyToAsync(copy, cancellationToken);
            return copy.ToArray();
        }

        /// <summary>Nén lại JPEG để nhúng vào PDF (ImageSharp). Quality ≤ 0 hoặc ≥ 100 = giữ nguyên; lỗi hoặc không nhỏ hơn → giữ ảnh gốc.</summary>
        private byte[] RecompressJpegForPdf(byte[] jpegBytes, int pageNumber)
        {
            var quality = _configuration.GetValue("Ocr:PdfJpegQuality", 80);
            if (quality <= 0 || quality >= 100) return jpegBytes;
            try
            {
                using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgb24>(jpegBytes);
                using var output = new MemoryStream();
                SixLabors.ImageSharp.ImageExtensions.SaveAsJpeg(image, output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality });
                return output.Length < jpegBytes.Length ? output.ToArray() : jpegBytes;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không nén lại được ảnh trang {Page} — dùng ảnh gốc.", pageNumber);
                return jpegBytes;
            }
        }

        /// <summary>Luồng đích cho PDF 2 lớp: file tạm tự xoá khi đóng; không tạo được thì dùng MemoryStream (hành vi cũ).</summary>
        private Stream CreateTempOutputStream(Guid fileId)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"ocr-{fileId:N}-{Guid.NewGuid():N}.pdf");
                return new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không tạo được file tạm cho PDF 2 lớp (file {FileId}) — ghi vào bộ nhớ.", fileId);
                return new MemoryStream();
            }
        }

        /// <summary>Ghi log mức dùng bộ nhớ (heap .NET + working set) để theo dõi đỉnh RAM của job lớn.</summary>
        private void LogMemory(string stage, Guid fileId, int page, long pdfBytes)
        {
            try
            {
                var heapMb = GC.GetTotalMemory(false) / 1048576;
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSetMb = process.WorkingSet64 / 1048576;
                _logger.LogInformation(
                    "Bộ nhớ OCR [{Stage}] file {FileId}: heap .NET {HeapMb} MB, working set {WorkingSetMb} MB (PDF nguồn {PdfMb} MB).",
                    stage, fileId, heapMb, workingSetMb, pdfBytes / 1048576);
            }
            catch { /* log chẩn đoán — không được làm hỏng job */ }
        }

        private static void ReleaseLargeObjectMemory()
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>Danh sách file tạm của các lô trang — tự xoá khi Dispose (kể cả khi job lỗi giữa chừng).</summary>
        private sealed class TempFileList : IDisposable
        {
            public List<string> Paths { get; } = new();

            public void Dispose()
            {
                foreach (var path in Paths)
                {
                    try { File.Delete(path); } catch { /* file tạm — dọn nỗ lực tối đa */ }
                }
                Paths.Clear();
            }
        }

        /// <summary>Lưu lô trang hiện tại ra file tạm rồi giải phóng ảnh đã giải mã của các trang trong lô (an toàn sau Save — xem AddPage).</summary>
        private static void SaveChunkAndRelease(PdfDocument chunk, ISearchablePdfBuilder pdfBuilder, TempFileList chunkFiles)
        {
            var path = Path.Combine(Path.GetTempPath(), $"ocr-chunk-{Guid.NewGuid():N}.pdf");
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
            {
                chunk.Save(fs, false);
            }
            chunkFiles.Paths.Add(path);
            pdfBuilder.ReleasePageResources();
        }

        /// <summary>Ghép các lô (đã lưu) thành một PdfDocument: nhập trang (chỉ byte JPEG nén, không giải mã lại) theo thứ tự.</summary>
        private static PdfDocument MergeChunks(TempFileList chunkFiles)
        {
            var merged = new PdfDocument();
            foreach (var path in chunkFiles.Paths)
            {
                using var source = PdfSharpCore.Pdf.IO.PdfReader.Open(path, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import);
                foreach (PdfSharpCore.Pdf.PdfPage page in source.Pages)
                {
                    merged.AddPage(page);
                }
            }
            return merged;
        }

        private const int MaxRetries = 3;

        /// <summary>Lỗi do dữ liệu đầu vào (không phải hạ tầng) — thử lại không thể thành công, đi thẳng DLQ + báo Failed.</summary>
        private sealed class NonRetryableOcrException : Exception
        {
            public NonRetryableOcrException(string message) : base(message) { }
        }

        private static bool LooksLikePdf(byte[] bytes)
        {
            // Đặc tả cho phép rác trước header trong 1024 byte đầu.
            var limit = Math.Min(bytes.Length - 4, 1024);
            for (var i = 0; i <= limit; i++)
            {
                if (bytes[i] == 0x25 && bytes[i + 1] == 0x50 && bytes[i + 2] == 0x44 && bytes[i + 3] == 0x46)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Lỗi ở cấp tài liệu (ngoài vòng lặp OCR từng trang — ví dụ tải file MinIO thất bại, PDF hỏng,
        /// upload/publish thất bại). Thử lại tối đa <see cref="MaxRetries"/> lần bằng cách ack message gốc
        /// rồi tự publish lại (đếm qua header "x-retry-count"); vượt quá thì đẩy nguyên message sang hàng
        /// đợi lỗi (DLQ) để kiểm tra thủ công, đồng thời báo ngay cho EquipmentService để đánh dấu Failed
        /// (không phải đợi watchdog quét theo thời gian).
        /// </summary>
        private async Task HandleTaskFailureAsync(
            BasicDeliverEventArgs ea,
            byte[] body,
            string messageText,
            Exception ex,
            CancellationToken cancellationToken)
        {
            var retryCount = GetRetryCount(ea.BasicProperties?.Headers) + 1;
            var isOutOfMemory = ex is OutOfMemoryException || ex.InnerException is OutOfMemoryException;
            // Hết bộ nhớ: thử lại cùng một file chắc chắn lặp lại (và chiếm GPU/RAM vô ích) → đi thẳng DLQ như lỗi dữ liệu đầu vào.
            var isFinalAttempt = retryCount > MaxRetries || ex is NonRetryableOcrException || isOutOfMemory;
            var targetRoutingKey = isFinalAttempt
                ? DigitizationTopicTopology.OcrTaskDeadLetterRoutingKey
                : _lane.RoutingKey; // thử lại ở đúng luồng đang xử lý (luồng lớn thử lại ở luồng lớn)

            // Publish/republish TRƯỚC, chỉ ack message gốc SAU KHI thành công — nếu đảo ngược thứ tự,
            // ack thất bại-republish sẽ làm mất message vĩnh viễn (đã ack nhưng bản republish chưa vào queue).
            var republished = await RepublishRawAsync(body, targetRoutingKey, retryCount, cancellationToken);
            if (!republished)
            {
                _logger.LogError(
                    "Không thể publish lại/DLQ message OCR (routing key {RoutingKey}) — trả message về queue gốc (nack requeue) để không mất dữ liệu.",
                    targetRoutingKey);
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken);
                return;
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);

            if (!isFinalAttempt)
            {
                _logger.LogWarning("OCR task lỗi, thử lại lần {RetryCount}/{MaxRetries}.", retryCount, MaxRetries);
                return;
            }

            if (isOutOfMemory)
            {
                _logger.LogError(ex, "OCR task hết bộ nhớ (OutOfMemoryException) — không thử lại, đã chuyển sang hàng đợi lỗi {DlqQueue}.",
                    DigitizationTopicTopology.OcrTaskDeadLetterQueue);
            }
            else if (ex is NonRetryableOcrException)
            {
                _logger.LogError(
                    "OCR task lỗi dữ liệu đầu vào (không thể retry) — đã chuyển thẳng sang hàng đợi lỗi {DlqQueue}: {Reason}",
                    DigitizationTopicTopology.OcrTaskDeadLetterQueue, ex.Message);
            }
            else
            {
                _logger.LogError(
                    "OCR task vượt quá {MaxRetries} lần thử — đã chuyển sang hàng đợi lỗi {DlqQueue}.",
                    MaxRetries, DigitizationTopicTopology.OcrTaskDeadLetterQueue);
            }

            var fileId = TryExtractFileId(messageText);
            if (fileId.HasValue)
            {
                using var scope = _serviceProvider.CreateScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
                var failedMsg = new
                {
                    FileId = fileId.Value,
                    Action = "ocr.process.failed",
                    CurrentPage = 0,
                    TotalPages = 0,
                    Progress = 0,
                    ErrorMessage = isOutOfMemory
                        ? "Hết bộ nhớ khi xử lý tài liệu (file hoặc số trang quá lớn) — hãy tách thành các file nhỏ hơn rồi tải lại, hoặc tăng bộ nhớ cho DigitizationService."
                        : ex.Message
                };
                // Thử lại có backoff — đây là tín hiệu duy nhất để EquipmentService đánh Failed ngay lập
                // tức; nếu vẫn thất bại sau cùng, watchdog (dựa trên ModifiedDate, độc lập RabbitMQ) sẽ
                // tự đóng job này sau tối đa 30 phút, nên không cần throw/chặn ở đây.
                await publisher.TryPublishMessageAsync(failedMsg, "digitization.topic", "ocr.process.progress",
                    maxAttempts: 3, initialDelay: TimeSpan.FromSeconds(2));
            }
        }

        /// <summary>
        /// Chuyển một tác vụ sang luồng lớn: publish TRƯỚC, ack message gốc SAU (giữ x-retry-count; job vẫn "Pending", EquipmentService không biết gì).
        /// Trả true nếu đã bàn giao (caller dừng xử lý); false nếu publish lỗi → caller xử lý ngay tại luồng nhỏ để không mất tác vụ.
        /// </summary>
        private async Task<bool> TryRedirectToLargeLaneAsync(
            BasicDeliverEventArgs ea, byte[] body, Guid fileId, int pageCount, long fileBytes, string reason, CancellationToken cancellationToken)
        {
            var published = await RepublishRawAsync(
                body, DigitizationTopicTopology.OcrTaskLargeRoutingKey, GetRetryCount(ea.BasicProperties?.Headers), cancellationToken);
            if (!published)
            {
                _logger.LogWarning(
                    "Không chuyển được tài liệu {FileId} sang luồng lớn ({Reason}) — xử lý ngay tại luồng nhỏ để không mất tác vụ.", fileId, reason);
                return false;
            }

            _logger.LogInformation(
                "Tài liệu {FileId} ({PageCount} trang / {SizeMb} MB) — chuyển sang luồng lớn {Queue} [{Reason}].",
                fileId, pageCount, fileBytes / 1048576, DigitizationTopicTopology.OcrTaskLargeQueue, reason);
            await AckAfterHandoffAsync(ea, cancellationToken);
            return true;
        }

        /// <summary>Ack message gốc SAU KHI đã bàn giao bản sao sang queue khác. Lỗi ack chỉ được ghi log và KHÔNG ném ra: nếu ném, handler chung sẽ publish
        /// thêm một bản retry (HandleTaskFailureAsync) trong khi RabbitMQ cũng giao lại message gốc → nhân bản tác vụ. (Giao lại ít nhất một lần là bản chất của AMQP;
        /// phía OCR có dấu /EvnOcrVersion chống dựng PDF 2 lớp lần hai.)</summary>
        private async Task AckAfterHandoffAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
        {
            try
            {
                await _channel!.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
            }
            catch (Exception ackEx)
            {
                _logger.LogWarning(ackEx,
                    "Ack message OCR thất bại sau khi đã bàn giao sang queue khác — message gốc sẽ được RabbitMQ giao lại (bản bàn giao vẫn nằm trong queue đích).");
            }
        }

        /// <summary>Xả một message của luồng lớn về queue chính (giữ x-retry-count). Mọi lỗi đều được bắt tại đây (handler này chạy ngoài try/catch chung).
        /// Lỗi publish → nack requeue sau một nhịp nghỉ để tránh vòng lặp nóng.</summary>
        private async Task DrainToStandardLaneAsync(BasicDeliverEventArgs ea, byte[] body, CancellationToken cancellationToken)
        {
            try
            {
                var moved = await RepublishRawAsync(
                    body, DigitizationTopicTopology.OcrTaskRoutingKey, GetRetryCount(ea.BasicProperties?.Headers), cancellationToken);
                if (moved)
                {
                    _logger.LogInformation("Luồng lớn đang TẮT (Ocr:LargeLaneEnabled=false) — đã trả tác vụ về {Queue}.", DigitizationTopicTopology.OcrTaskQueue);
                    await AckAfterHandoffAsync(ea, cancellationToken);
                    return;
                }

                _logger.LogWarning("Không xả được tác vụ về {Queue} — trả lại luồng lớn (nack requeue) sau 2 giây.", DigitizationTopicTopology.OcrTaskQueue);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Đang tắt service — message chưa ack sẽ được RabbitMQ giao lại khi channel đóng.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Xả tác vụ OCR khỏi luồng lớn thất bại — message chưa ack sẽ được RabbitMQ giao lại khi channel đóng.");
            }
        }

        private async Task<bool> RepublishRawAsync(byte[] body, string routingKey, int retryCount, CancellationToken cancellationToken)
        {
            try
            {
                var props = new BasicProperties
                {
                    // Persistent: message gửi lại/chuyển luồng/DLQ phải sống sót khi RabbitMQ restart (queue đã durable).
                    Persistent = true,
                    ContentType = "application/json",
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
                var task = JsonSerializer.Deserialize<OcrTaskMessage>(messageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return task?.FileId;
            }
            catch
            {
                return null;
            }
        }
    }
}
