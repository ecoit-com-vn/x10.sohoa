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
using EvnHanoi.DigitizationService.Services;
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
        private IChannel? _channel;
        private readonly string _ocrVlServerUrl;
        private readonly string _llmServerUrl;

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
            _ocrVlServerUrl = _configuration["AIModelServers:OcrVlServerUrl"] ?? "http://ocr3.ecoit.asia";
            _llmServerUrl = _configuration["AIModelServers:LlmServerUrl"] ?? "http://localhost:8080";
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

                // Giới hạn số message xử lý đồng thời/1 kết nối bằng đúng ConsumerDispatchConcurrency
                // (xem Program.cs) — để RabbitMQ không dồn quá nhiều message chưa ack cho worker.
                //
                // prefetch 2 (hạ từ 4) khớp ConsumerDispatchConcurrency=2. Mỗi tài liệu OCR chiếm
                // GPU rất lâu (tài liệu thật 96–744 vùng chữ/trang, ~4,4 crop/giây) nên giữ nhiều
                // message unacked chỉ làm chúng chờ tới lúc vượt timeout, không giúp nhanh hơn.
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 2, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);
                    //_logger.LogInformation("Nhận yêu cầu OCR: {Message}", messageText);

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

                            // Bàn giao sang ExtractionWorker — dùng chung cho luồng bình thường và
                            // luồng bỏ qua khi phát hiện file đã là PDF 2 lớp (idempotency, xem dưới).
                            async Task PublishExtractionAndAckAsync(string extractionFilePath)
                            {
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

                            // 1. Tải file PDF từ MinIO
                            _logger.LogInformation("Tải file {FilePath} từ bucket {BucketName}", taskMsg.FilePath, taskMsg.BucketName);
                            using var fileStream = await minioService.DownloadFileAsync(taskMsg.BucketName, taskMsg.FilePath);

                            using var msPdf = new MemoryStream();
                            await fileStream.CopyToAsync(msPdf, stoppingToken);
                            byte[] pdfBytes = msPdf.ToArray();

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

                            using var pdfDocument = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);

                            // Timeout thực tế do AddStandardResilienceHandler kiểm soát (xem Program.cs, cấu hình qua AIModelServers:OcrPage*)
                            var httpClient = httpClientFactory.CreateClient("OcrPageClient");
                            httpClient.Timeout = Timeout.InfiniteTimeSpan;

                            var llmClient = httpClientFactory.CreateClient("LlmClient");
                            llmClient.BaseAddress = new Uri(_llmServerUrl);
                            llmClient.Timeout = TimeSpan.FromMinutes(5);

                            // Tạo PDF 2 lớp output
                            using var outPdfDoc = new PdfDocument();
                            int totalBoxesDrawn = 0;

                            for (int i = 0; i < pageCount; i++)
                            {
                                _logger.LogInformation("Đang xử lý trang {Page}/{TotalPages}...", i + 1, pageCount);

                                // 2. Render trang PDF → JPEG (200 DPI)
                                using var imgStream = new MemoryStream();
                                var renderOptions = new PDFtoImage.RenderOptions { Dpi = 200, WithAnnotations = true };
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
                                totalBoxesDrawn += pdfBuilder.AddPage(outPdfDoc, pageImageBytes, ocrResults);

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
                            pdfBuilder.MarkAsSearchable(outPdfDoc);

                            // 5. Upload PDF 2 lớp lên MinIO (ghi đè file gốc)
                            string outFileName = taskMsg.FilePath;
                            using var finalPdfStream = new MemoryStream();
                            outPdfDoc.Save(finalPdfStream, false);
                            finalPdfStream.Position = 0;

                            _logger.LogInformation("Đang upload (ghi đè) PDF 2 lớp {FileName} lên MinIO", outFileName);
                            await minioService.UploadFileAsync(taskMsg.BucketName, outFileName, finalPdfStream, "application/pdf");

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

        private const int MaxRetries = 3;

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
            var isFinalAttempt = retryCount > MaxRetries;
            var targetRoutingKey = isFinalAttempt
                ? DigitizationTopicTopology.OcrTaskDeadLetterRoutingKey
                : DigitizationTopicTopology.OcrTaskRoutingKey;

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

            _logger.LogError(
                "OCR task vượt quá {MaxRetries} lần thử — đã chuyển sang hàng đợi lỗi {DlqQueue}.",
                MaxRetries, DigitizationTopicTopology.OcrTaskDeadLetterQueue);

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
                    ErrorMessage = ex.Message
                };
                // Thử lại có backoff — đây là tín hiệu duy nhất để EquipmentService đánh Failed ngay lập
                // tức; nếu vẫn thất bại sau cùng, watchdog (dựa trên ModifiedDate, độc lập RabbitMQ) sẽ
                // tự đóng job này sau tối đa 30 phút, nên không cần throw/chặn ở đây.
                await publisher.TryPublishMessageAsync(failedMsg, "digitization.topic", "ocr.process.progress",
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
