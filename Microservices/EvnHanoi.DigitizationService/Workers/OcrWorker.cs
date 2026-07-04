// e:/ecoit/sohoax10/sohoa.backend/Microservices/EvnHanoi.DigitizationService/Workers/OcrWorker.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json.Serialization;
using PdfDocument = PdfSharpCore.Pdf.PdfDocument;
using PdfPage = PdfSharpCore.Pdf.PdfPage;
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
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;

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
                                    FormSchemaJson = taskMsg.FormSchemaJson
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

                            int pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
                            _logger.LogInformation("PDF có {PageCount} trang. Bắt đầu xử lý từng trang.", pageCount);

                            // HttpClient không timeout — ocr_vl_server có thể mất vài giây
                            var httpClient = httpClientFactory.CreateClient("NoTimeout");
                            httpClient.Timeout = Timeout.InfiniteTimeSpan;

                            var llmClient = httpClientFactory.CreateClient("LlmClient");
                            llmClient.BaseAddress = new Uri(_llmServerUrl);
                            llmClient.Timeout = TimeSpan.FromMinutes(5);

                            // Tạo PDF 2 lớp output
                            using var outPdfDoc = new PdfDocument();
                            // Brush gần trong suốt để text ẩn nhưng vẫn searchable
                            XBrush transparentBrush = new XSolidBrush(XColor.FromArgb(1, 0, 0, 0));

                            for (int i = 0; i < pageCount; i++)
                            {
                                _logger.LogInformation("Đang xử lý trang {Page}/{TotalPages}...", i + 1, pageCount);

                                // 2. Render trang PDF → JPEG (150 DPI)
                                using var imgStream = new MemoryStream();
                                var renderOptions = new PDFtoImage.RenderOptions { Dpi = 150 };
                                PDFtoImage.Conversion.SaveJpeg(imgStream, pdfBytes, password: null, page: i, options: renderOptions);
                                byte[] pageImageBytes = imgStream.ToArray();

                                List<TextBoxResponse> ocrResults = new();
                                var sw = Stopwatch.StartNew();

                                try
                                {
                                    // 3. Gửi ảnh toàn trang lên ocr_vl_server → nhận [{text, box, confidence}]
                                    //    Server thực hiện: PaddleOCR detect + 1 LLM call "OCR:"
                                    using var multipart = new MultipartFormDataContent();
                                    var imageContent = new ByteArrayContent(pageImageBytes);
                                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                                    multipart.Add(imageContent, "file", $"page_{i + 1}.jpg");

                                    string ocrPageUrl = $"{_ocrVlServerUrl.TrimEnd('/')}/ocr_page";
                                    _logger.LogInformation("Gửi trang {Page} lên {Url}", i + 1, ocrPageUrl);

                                    var response = await httpClient.PostAsync(ocrPageUrl, multipart, stoppingToken);

                                    if (response.IsSuccessStatusCode)
                                    {
                                        var respStr = await response.Content.ReadAsStringAsync(stoppingToken);
                                        var boxes = JsonSerializer.Deserialize<List<TextBoxResponse>>(respStr,
                                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                        if (boxes != null)
                                            ocrResults = boxes;
                                    }
                                    else
                                    {
                                        _logger.LogWarning("ocr_vl_server trả về lỗi {StatusCode} cho trang {Page}", response.StatusCode, i + 1);
                                    }

                                    sw.Stop();
                                    _logger.LogInformation("[ĐO ĐẠC] Trang {Page} hoàn thành OCR sau {ElapsedMs} ms. Nhận về {Count} box text.",
                                        i + 1, sw.ElapsedMilliseconds, ocrResults.Count);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Lỗi khi gọi ocr_vl_server cho trang {Page}.", i + 1);
                                }

                                // 3b. Sửa chính tả từng box bằng LLM (trước khi vẽ PDF và sinh MD)
                                if (ocrResults.Any())
                                {
                                    // Lọc các box hợp lệ để gửi cho LLM
                                    var validBoxes = ocrResults
                                        .Where(b => b.Box != null && b.Box.Count == 4 && !string.IsNullOrWhiteSpace(b.Text))
                                        .ToList();

                                    if (validBoxes.Any())
                                    {
                                        try
                                        {
                                            _logger.LogInformation("Đang gọi LLM sửa chính tả cho {Count} box text trang {Page}...", validBoxes.Count, i + 1);

                                            // Chuẩn bị mảng JSON gửi cho LLM
                                            var textsToCorrect = validBoxes
                                                .Select((b, idx) => new { index = idx, text = b.Text })
                                                .ToList();
                                            string inputJson = JsonSerializer.Serialize(textsToCorrect);

                                            var correctionPrompt = @"Bạn là chuyên gia hiệu đính văn bản tiếng Việt bị lỗi OCR trong lĩnh vực điện lực.

NHIỆM VỤ: Nhận vào mảng JSON chứa các đoạn text bị lỗi OCR. Sửa chính tả từng đoạn và trả về mảng JSON CÙNG SỐ LƯỢNG phần tử, CÙNG THỨ TỰ index.

CÁC LOẠI LỖI CẦN SỬA:
A. LỖI DẤU THANH: KỶ→KỸ, SỰA→SỬA, LÓN→LỚN, TÀI→TẠI, MẮT→MẤT, LỤC→LỰC, PHƯƠNG ẢN→PHƯƠNG ÁN, NHỊM→NHIỆM
B. LỖI MẤT DẤU PHỤ: son→sơn, gi→gỉ, QLDT→QLĐT, mất dấu mũ/móc/ngang
C. LỖI NHẦM KÝ TỰ: nối→nói, dột→đột, Trưởng→Trường, SỎI→SỐI, CHÈM→CHÊM
D. LỖI THỪA/THIẾU: UUY BAN→ỦY BAN, UỞ BAN->ỦY BAN, UỬ BAN->ỦY BAN
E. LỖI ARTIFACTS: Loại bỏ LaTeX artifacts (\underline, \text{...})

QUY TẮC:
1. Sửa dựa trên NGỮ CẢNH. Ví dụ: 'thấm đột'→'thấm dột'; 'Trường phòng'→'Trưởng phòng'.
2. GIỮ NGUYÊN số liệu, đơn vị, mã kỹ thuật (22/0,4kV, TBA, QLĐT, MBA).
3. PHẢI trả về ĐÚNG số lượng phần tử và ĐÚNG thứ tự index.
4. CHỈ trả về mảng JSON. Không thêm giải thích, không bọc trong markdown code block.";

                                            var payload = new
                                            {
                                                messages = new[]
                                                {
                                                    new { role = "system", content = correctionPrompt },
                                                    new { role = "user", content = inputJson }
                                                },
                                                temperature = 0.1,
                                                max_tokens = 4000
                                            };

                                            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                                            var llmResponse = await llmClient.PostAsync("/v1/chat/completions", content, stoppingToken);

                                            if (llmResponse.IsSuccessStatusCode)
                                            {
                                                var resultStr = await llmResponse.Content.ReadAsStringAsync(stoppingToken);
                                                var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(resultStr);
                                                var llmOutput = jsonNode?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();

                                                if (!string.IsNullOrWhiteSpace(llmOutput))
                                                {
                                                    // Loại bỏ markdown code block nếu LLM tự thêm
                                                    llmOutput = llmOutput.Trim();
                                                    if (llmOutput.StartsWith("```json"))
                                                    {
                                                        llmOutput = llmOutput.Substring(7);
                                                        if (llmOutput.EndsWith("```")) llmOutput = llmOutput.Substring(0, llmOutput.Length - 3);
                                                    }
                                                    else if (llmOutput.StartsWith("```"))
                                                    {
                                                        llmOutput = llmOutput.Substring(3);
                                                        if (llmOutput.EndsWith("```")) llmOutput = llmOutput.Substring(0, llmOutput.Length - 3);
                                                    }
                                                    llmOutput = llmOutput.Trim();

                                                    // Parse mảng JSON trả về
                                                    var correctedItems = JsonSerializer.Deserialize<List<CorrectedTextItem>>(llmOutput,
                                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                                    if (correctedItems != null && correctedItems.Count == validBoxes.Count)
                                                    {
                                                        // Cập nhật text đã sửa ngược lại vào ocrResults
                                                        for (int ci = 0; ci < correctedItems.Count; ci++)
                                                        {
                                                            if (!string.IsNullOrWhiteSpace(correctedItems[ci].Text))
                                                            {
                                                                validBoxes[ci].Text = correctedItems[ci].Text;
                                                            }
                                                        }
                                                        _logger.LogInformation("Sửa chính tả {Count} box trang {Page} thành công.", correctedItems.Count, i + 1);
                                                    }
                                                    else
                                                    {
                                                        _logger.LogWarning("LLM trả về {ReturnCount} items nhưng cần {ExpectedCount}. Giữ nguyên text OCR gốc cho trang {Page}.",
                                                            correctedItems?.Count ?? 0, validBoxes.Count, i + 1);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning("LLM trả về status {StatusCode} khi sửa box trang {Page}. Sử dụng text OCR gốc.", llmResponse.StatusCode, i + 1);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Lỗi khi gọi LLM sửa chính tả box trang {Page}. Sử dụng text OCR gốc.", i + 1);
                                        }
                                    }
                                }

                                // 4. Tạo trang PDF 2 lớp: ảnh gốc + text ẩn (ĐÃ SỬA CHÍNH TẢ)
                                PdfPage newPage = outPdfDoc.AddPage();
                                using XGraphics gfx = XGraphics.FromPdfPage(newPage);

                                using var memStreamImg = new MemoryStream(pageImageBytes);
                                using XImage xImage = XImage.FromStream(() => memStreamImg);

                                // Quy đổi pixel → point (72pt = 1 inch; 150 DPI → scale = 72/150)
                                double scale = 72.0 / 150.0;
                                double imgWidthPx  = xImage.PixelWidth;
                                double imgHeightPx = xImage.PixelHeight;
                                newPage.Width  = imgWidthPx  * scale;
                                newPage.Height = imgHeightPx * scale;
                                gfx.DrawImage(xImage, 0, 0, newPage.Width, newPage.Height);

                                // Vẽ text ẩn (invisible text layer) — sử dụng text đã sửa chính tả
                                foreach (var boxData in ocrResults)
                                {
                                    if (boxData.Box == null || boxData.Box.Count != 4) continue;
                                    if (string.IsNullOrWhiteSpace(boxData.Text)) continue;

                                    double x0 = boxData.Box[0] * scale;
                                    double y0 = boxData.Box[1] * scale;
                                    double x1 = boxData.Box[2] * scale;
                                    double y1 = boxData.Box[3] * scale;

                                    double w = Math.Max(x1 - x0, 10 * scale);
                                    double h = Math.Max(y1 - y0, 6 * scale);

                                    // Font size tương ứng với chiều cao box (0.75 * h)
                                    double fontSize = Math.Max(4, h * 0.75);
                                    XFont font = new XFont("Open Sans", fontSize, XFontStyle.Regular);

                                    XRect rect = new XRect(x0, y0, w, h);
                                    gfx.DrawString(boxData.Text, font, transparentBrush, rect, XStringFormats.TopLeft);
                                }

                                // 4b. Sinh Markdown cho page — sử dụng text đã sửa chính tả
                                var mdLines = new List<string>();
                                if (ocrResults.Any())
                                {
                                    // Nhóm các box có y0 chênh lệch <= 10px thành cùng một dòng
                                    double yTolerance = 10.0;
                                    var lines = new List<List<TextBoxResponse>>();
                                    
                                    var sortedByY = ocrResults.Where(b => b.Box != null && b.Box.Count == 4 && !string.IsNullOrWhiteSpace(b.Text))
                                                              .OrderBy(b => b.Box[1]).ToList();
                                                              
                                    foreach (var box in sortedByY)
                                    {
                                        bool added = false;
                                        foreach (var line in lines)
                                        {
                                            double avgY = line.Average(b => b.Box[1]);
                                            if (Math.Abs(box.Box[1] - avgY) <= yTolerance)
                                            {
                                                line.Add(box);
                                                added = true;
                                                break;
                                            }
                                        }
                                        if (!added)
                                        {
                                            lines.Add(new List<TextBoxResponse> { box });
                                        }
                                    }
                                    
                                    foreach (var line in lines)
                                    {
                                        var sortedByX = line.OrderBy(b => b.Box[0]).ToList();
                                        
                                        var lineParts = new List<string>();
                                        foreach (var box in sortedByX)
                                        {
                                            string cleanText = Regex.Replace(box.Text, @"[ \t]{2,}", " ");
                                            cleanText = Regex.Replace(cleanText, @"\r\n|\r|\n", " ");
                                            lineParts.Add(cleanText.Trim());
                                        }
                                        mdLines.Add(string.Join(" ", lineParts));
                                    }
                                }

                                string pageMarkdown = string.Join("\n", mdLines);
                                pageMarkdown = Regex.Replace(pageMarkdown, @"\n{3,}", "\n\n"); // Max 2 newlines

                                // Upload Markdown file
                                string baseFilePath = taskMsg.FilePath;
                                if (baseFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                {
                                    baseFilePath = baseFilePath.Substring(0, baseFilePath.Length - 4);
                                }
                                string mdFileName = $"{baseFilePath}_page_{i + 1}.md";

                                using var mdStream = new MemoryStream(Encoding.UTF8.GetBytes(pageMarkdown));
                                await minioService.UploadFileAsync(taskMsg.BucketName, mdFileName, mdStream, "text/markdown");
                                _logger.LogInformation("Đã upload file markdown cho trang {Page}: {FileName}", i + 1, mdFileName);

                                // Báo cáo tiến trình per-page
                                var progressMsg = new
                                {
                                    FileId = taskMsg.FileId,
                                    Action = "ocr.process.progress",
                                    CurrentPage = i + 1,
                                    TotalPages = pageCount,
                                    Progress = (int)Math.Round((double)(i + 1) / pageCount * 100)
                                };
                                await publisher.PublishMessageAsync(progressMsg, "digitization.topic", "ocr.process.progress");
                            }

                            // 5. Upload PDF 2 lớp lên MinIO (ghi đè file gốc)
                            string outFileName = taskMsg.FilePath;
                            using var finalPdfStream = new MemoryStream();
                            outPdfDoc.Save(finalPdfStream, false);
                            finalPdfStream.Position = 0;

                            _logger.LogInformation("Đang upload (ghi đè) PDF 2 lớp {FileName} lên MinIO", outFileName);
                            await minioService.UploadFileAsync(taskMsg.BucketName, outFileName, finalPdfStream, "application/pdf");

                            // 6. Publish ExtractionTaskMessage → ExtractionWorker
                            var extractionTask = new ExtractionTaskMessage
                            {
                                FileId = taskMsg.FileId,
                                FilePath = outFileName,
                                BucketName = taskMsg.BucketName,
                                ExtractPrompt = taskMsg.ExtractPrompt,
                                Form = taskMsg.Form,
                                FormSchemaJson = taskMsg.FormSchemaJson
                            };
                            await publisher.PublishMessageAsync(extractionTask, "digitization.topic", "extraction.process.task");

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
