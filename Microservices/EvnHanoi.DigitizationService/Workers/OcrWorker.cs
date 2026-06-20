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
using System.Net.Http.Headers;
using System.IO;
using System.Collections.Generic;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;

namespace EvnHanoi.DigitizationService.Workers
{
    public class TextBoxResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("text")]
        public string Text { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("box")]
        public List<float> Box { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public float Confidence { get; set; }
    }

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
            // Trỏ thẳng đến FastAPI đã viết lại
            _ocrVlServerUrl = _configuration["AIModelServers:OcrVlServerUrl"] ?? "http://localhost:8091";
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
                            try {
                                await repository.UpdateStatusAsync(taskMsg.FileId, "Processing");
                                _logger.LogInformation("Đã cập nhật trạng thái FileAttachment {FileId} thành Processing.", taskMsg.FileId);
                            } catch (Exception ex) {
                                _logger.LogWarning("Bỏ qua lỗi DB khi update trạng thái: {Message}", ex.Message);
                            }

                            // 2. Tải file từ MinIO
                            _logger.LogInformation("Tải file {FilePath} từ bucket {BucketName}", taskMsg.FilePath, taskMsg.BucketName);
                            using var fileStream = await minioService.DownloadFileAsync(taskMsg.BucketName, taskMsg.FilePath);
                            
                            using var msPdf = new MemoryStream();
                            await fileStream.CopyToAsync(msPdf, stoppingToken);
                            byte[] pdfBytes = msPdf.ToArray();

                            int pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
                            _logger.LogInformation("PDF có {PageCount} trang. Bắt đầu xử lý từng trang.", pageCount);

                            var httpClient = httpClientFactory.CreateClient("OcrVlClient");
                            httpClient.BaseAddress = new Uri(_ocrVlServerUrl);
                            httpClient.Timeout = TimeSpan.FromMinutes(10); // set longer timeout

                            var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

                            // Tạo PdfDocument để lưu PDF 2 lớp
                            using var outPdfDoc = new PdfDocument();
                            // Brush trong suốt để ẩn text (alpha=1: gần như vô hình nhưng searchable)
                            XBrush transparentBrush = new XSolidBrush(XColor.FromArgb(1, 0, 0, 0)); 

                            for (int i = 0; i < pageCount; i++)
                            {
                                _logger.LogInformation("Đang render trang {Page}/{TotalPages} của {FileId}...", i + 1, pageCount, taskMsg.FileId);
                                using var imgStream = new MemoryStream();
                                PDFtoImage.Conversion.SaveJpeg(imgStream, pdfBytes, null, i); // options: null, page: i
                                byte[] pageImageBytes = imgStream.ToArray();

                                // Chuẩn bị request gọi FastAPI OCR
                                using var content = new MultipartFormDataContent();
                                var fileContent = new ByteArrayContent(pageImageBytes);
                                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                                content.Add(fileContent, "file", $"page_{i}.jpg");

                                List<TextBoxResponse> ocrResults = new List<TextBoxResponse>();
                                try
                                {
                                    _logger.LogInformation("Đang gửi trang {Page} tới ocr_vl_server (FastAPI)...", i + 1);
                                    var response = await httpClient.PostAsync("/ocr_image", content, stoppingToken);
                                    response.EnsureSuccessStatusCode();
                                    
                                    var ocrResultText = await response.Content.ReadAsStringAsync(stoppingToken);
                                    ocrResults = JsonSerializer.Deserialize<List<TextBoxResponse>>(ocrResultText) ?? new List<TextBoxResponse>();
                                    _logger.LogInformation("Nhận về {Count} box text cho trang {Page}.", ocrResults.Count, i + 1);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Lỗi khi gọi FastAPI OCR cho trang {Page}.", i + 1);
                                }

                                // Tạo trang PDF mới và chèn ảnh + text ẩn
                                PdfPage newPage = outPdfDoc.AddPage();
                                using XGraphics gfx = XGraphics.FromPdfPage(newPage);
                                
                                using var memStreamImg = new MemoryStream(pageImageBytes);
                                using XImage xImage = XImage.FromStream(() => memStreamImg);
                                
                                // Ảnh gốc có kích thước pixel, PDF dùng đơn vị point (1px = 1pt ở đây)
                                double imgWidthPx  = xImage.PixelWidth;
                                double imgHeightPx = xImage.PixelHeight;
                                newPage.Width  = imgWidthPx;
                                newPage.Height = imgHeightPx;
                                gfx.DrawImage(xImage, 0, 0, newPage.Width, newPage.Height);

                                foreach (var boxData in ocrResults)
                                {
                                    if (boxData.Box != null && boxData.Box.Count == 4)
                                    {
                                        double x0 = boxData.Box[0];
                                        double y0 = boxData.Box[1];
                                        double x1 = boxData.Box[2];
                                        double y1 = boxData.Box[3];
                                        
                                        double w = x1 - x0;
                                        double h = y1 - y0;
                                        if (w <= 0) w = 10;
                                        if (h <= 0) h = 10;

                                        // Tính font size dựa trên chiều cao box để khớp với từng dòng text
                                        double fontSize = Math.Max(4, h * 0.75);
                                        XFont font = new XFont("Arial", fontSize, XFontStyle.Regular);

                                        XRect rect = new XRect(x0, y0, w, h);
                                        // Vẽ text tàng hình (searchable) lên trên hình ảnh
                                        gfx.DrawString(boxData.Text ?? "", font, transparentBrush, rect, XStringFormats.TopLeft);
                                    }
                                }

                                // Báo cáo tiến trình (Progress)
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

                            // 4. Lưu PDF 2 lớp và Upload lên MinIO (ghi đè file gốc để tiết kiệm dung lượng)
                            string outFileName = taskMsg.FilePath;
                            using var finalPdfStream = new MemoryStream();
                            outPdfDoc.Save(finalPdfStream, false);
                            finalPdfStream.Position = 0;
                            
                            _logger.LogInformation("Đang upload (ghi đè) PDF 2 lớp {FileName} lên MinIO", outFileName);
                            await minioService.UploadFileAsync(taskMsg.BucketName, outFileName, finalPdfStream, "application/pdf");

                            // 5. Cập nhật trạng thái DB
                            try {
                                await repository.UpdateStatusAsync(taskMsg.FileId, "OcrCompleted");
                                _logger.LogInformation("AI OCR & Đóng PDF đã xong. Cập nhật trạng thái FileAttachment {FileId} thành OcrCompleted.", taskMsg.FileId);
                            } catch (Exception ex) {
                                _logger.LogWarning("Bỏ qua lỗi DB khi update trạng thái: {Message}", ex.Message);
                            }

                            // 6. Publish ExtractionTaskMessage
                            var extractionTask = new ExtractionTaskMessage
                            {
                                FileId = taskMsg.FileId,
                                FilePath = outFileName,
                                BucketName = taskMsg.BucketName,
                                Forms = taskMsg.Forms ?? new List<ExtractionForm>()
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
