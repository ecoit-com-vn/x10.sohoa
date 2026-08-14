using System.Text;
using System.Text.Json;
using EvnHanoi.DigitizationService.Helpers;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.DigitizationService.Services.OcrModule;
using EvnHanoi.DigitizationService.Workers;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace EvnHanoi.DigitizationService.Core.Services.OcrModule;

/// <summary>Region chưa có SourceIndex (materialize trước khi hỗ trợ sửa trực tiếp) — không patch
/// ngược được vào file JSON gốc trên MinIO, không tự suy đoán vị trí để tránh ghi sai box.</summary>
public class RegionSourceIndexMissingException : Exception
{
    public RegionSourceIndexMissingException(string regionId)
        : base($"Region {regionId} không có SourceIndex — được tạo trước khi hỗ trợ sửa trực tiếp.")
    {
    }
}

/// <summary>
/// Yêu cầu bổ sung của tab "Kiểm tra chính tả và hiệu chỉnh nội dung" — sửa tay nội dung 1 box OCR
/// và đồng bộ NGƯỢC lại tài liệu thật, khác với việc chỉ ghi vào OCR_MODULE_REGION (bản snapshot
/// materialize 1 lần từ MinIO, đọc-là-chính, không tự lan lại). Dùng chung cho cả 2 đường: sửa tay
/// tự do và chấp nhận gợi ý AI (OcrModuleAnalysisController.UpdateSpellcheckStatus khi Status là
/// Accepted/ManuallyEdited) — chỉ khác giá trị <see cref="ApplyManualCorrectionAsync"/> nhận vào.
///
/// Thứ tự xử lý CỐ Ý: patch MinIO JSON + dựng lại PDF trang TRƯỚC, xác nhận OCR_MODULE_REGION SAU —
/// để nếu bước MinIO/PDF lỗi thì không lỡ báo "đã lưu" cho 1 sửa chưa hề chạm tới tài liệu thật (đúng
/// lỗi cũ đang sửa: nút "sửa tay"/"chấp nhận gợi ý" trước đây chỉ ghi DB, không lan ra MinIO/PDF/ES).
/// </summary>
public interface IOcrModuleRegionCorrectionService
{
    Task<OcrModuleRegionDto> ApplyManualCorrectionAsync(
        string jobId, string regionId, string newText, string? spellcheckStatus, string? editedBy);
}

public class OcrModuleRegionCorrectionService : IOcrModuleRegionCorrectionService
{
    private readonly IOcrModuleRepository _repository;
    private readonly IMinioStorageService _minioService;
    private readonly ISearchablePdfBuilder _pdfBuilder;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<OcrModuleRegionCorrectionService> _logger;

    public OcrModuleRegionCorrectionService(
        IOcrModuleRepository repository,
        IMinioStorageService minioService,
        ISearchablePdfBuilder pdfBuilder,
        IMessagePublisher messagePublisher,
        ILogger<OcrModuleRegionCorrectionService> logger)
    {
        _repository = repository;
        _minioService = minioService;
        _pdfBuilder = pdfBuilder;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task<OcrModuleRegionDto> ApplyManualCorrectionAsync(
        string jobId, string regionId, string newText, string? spellcheckStatus, string? editedBy)
    {
        var region = await _repository.GetRegionByIdAsync(regionId)
            ?? throw new KeyNotFoundException($"Không tìm thấy region {regionId}.");

        if (region.SourceIndex == null)
            throw new RegionSourceIndexMissingException(regionId);

        var job = await _repository.GetJobByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Không tìm thấy Job {jobId}.");

        var normalizedText = OcrPageContentHelper.NormalizeUtf8Text(newText);

        // 1. Patch file JSON gốc của trang trên MinIO — cùng quy ước tên file OcrWorker đã dùng khi OCR.
        var baseFilePath = job.SourceFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? job.SourceFilePath[..^4]
            : job.SourceFilePath;
        var jsonFileName = $"{baseFilePath}_page_{region.PageNumber}.json";

        List<TextBoxResponse> pageBoxes;
        using (var jsonStream = await _minioService.DownloadFileAsync(job.SourceBucket, jsonFileName))
        using (var reader = new StreamReader(jsonStream))
        {
            var pageJson = await reader.ReadToEndAsync();
            pageBoxes = OcrPageContentHelper.DeserializeOcrResponse(pageJson);
        }

        if (region.SourceIndex.Value < 0 || region.SourceIndex.Value >= pageBoxes.Count)
        {
            throw new InvalidOperationException(
                $"SourceIndex {region.SourceIndex} của region {regionId} không còn khớp với file JSON hiện " +
                $"tại của trang {region.PageNumber} ({pageBoxes.Count} box).");
        }

        pageBoxes[region.SourceIndex.Value].Text = normalizedText;

        var patchedJson = JsonSerializer.Serialize(pageBoxes, OcrPageContentHelper.OcrJsonOptions);
        using (var patchedStream = new MemoryStream(Encoding.UTF8.GetBytes(patchedJson)))
        {
            await _minioService.UploadFileAsync(job.SourceBucket, jsonFileName, patchedStream, "application/json");
        }

        // 2. Dựng lại đúng 1 trang PDF (các trang khác import nguyên, không render lại) và ghi đè.
        await RebuildSinglePageAsync(job, region.PageNumber, pageBoxes);

        // 3. Xác nhận vào OCR_MODULE_REGION — CHỈ SAU KHI bước 1/2 đã thành công.
        await _repository.UpdateRegionTextAndStatusAsync(regionId, normalizedText, spellcheckStatus, editedBy);

        // 4. Đồng bộ lại Elasticsearch — luôn publish bất kể trạng thái xuất bản của hồ sơ (quyết định
        // đã chốt khi lập kế hoạch). Lỗi publish chỉ log warning, không làm hỏng việc lưu vừa xong.
        await PublishReindexAsync(job);

        return await _repository.GetRegionDtoByIdAsync(regionId)
            ?? throw new InvalidOperationException($"Không đọc lại được region {regionId} sau khi lưu.");
    }

    private async Task RebuildSinglePageAsync(OcrModuleJob job, int pageNumber, List<TextBoxResponse> patchedBoxesThisPage)
    {
        using var pdfStream = await _minioService.DownloadFileAsync(job.SourceBucket, job.SourceFilePath);
        using var msPdf = new MemoryStream();
        await pdfStream.CopyToAsync(msPdf);
        var pdfBytes = msPdf.ToArray();

        // srcDoc phải sống tới khi outDoc.Save() chạy xong — khai báo trước outDoc để thứ tự Dispose
        // (LIFO của "using") giải phóng outDoc trước, srcDoc sau.
        using var srcDoc = PdfReader.Open(new MemoryStream(pdfBytes), PdfDocumentOpenMode.Import);
        using var outDoc = new PdfDocument();

        for (var i = 0; i < srcDoc.PageCount; i++)
        {
            if (i + 1 != pageNumber)
            {
                outDoc.AddPage(srcDoc.Pages[i]);
                continue;
            }

            using var imgStream = new MemoryStream();
            var renderOptions = new PDFtoImage.RenderOptions { Dpi = OcrModuleImageDpi.OcrSourceDpi, WithAnnotations = true };
            PDFtoImage.Conversion.SaveJpeg(imgStream, pdfBytes, password: null, page: i, options: renderOptions);

            _pdfBuilder.AddPage(outDoc, imgStream.ToArray(), patchedBoxesThisPage, dpi: OcrModuleImageDpi.OcrSourceDpi);
        }

        _pdfBuilder.MarkAsSearchable(outDoc);

        using var finalStream = new MemoryStream();
        outDoc.Save(finalStream, false);
        finalStream.Position = 0;

        await _minioService.UploadFileAsync(job.SourceBucket, job.SourceFilePath, finalStream, "application/pdf");
    }

    private async Task PublishReindexAsync(OcrModuleJob job)
    {
        // Job tạo từ upload mới (không gắn tài liệu hồ sơ/thiết bị nào) — không có gì để reindex.
        if (string.IsNullOrWhiteSpace(job.SourceDocumentVersionId))
            return;

        try
        {
            var evt = new DocumentTextIndexEvent(
                job.SourceDocumentVersionId,
                job.SourceBucket,
                job.SourceFilePath,
                job.TotalPages,
                DocumentTextIndexActions.Index,
                DateTime.UtcNow);

            await _messagePublisher.TryPublishMessageAsync(
                evt, DigitizationTopicTopology.ExchangeName, DocumentTextMessaging.ReindexRoutingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Không publish được sự kiện index lại tài liệu {VersionId} sau khi sửa OCR — MinIO/PDF/DB đã " +
                "lưu thành công, chỉ Elasticsearch tạm thời chưa khớp.",
                job.SourceDocumentVersionId);
        }
    }
}
