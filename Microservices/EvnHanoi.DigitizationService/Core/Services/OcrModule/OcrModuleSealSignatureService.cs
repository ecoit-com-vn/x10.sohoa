using EvnHanoi.DigitizationService.Core.ImageProcessing;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.Infrastructure.Database;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EvnHanoi.DigitizationService.Core.Services.OcrModule;

public class SealSignatureRunResult
{
    public int SealCount { get; set; }
    public int SignatureCount { get; set; }
}

/// <summary>
/// Yêu cầu 94 — điều phối việc dựng lại ảnh từng trang từ PDF gốc đã lưu trên MinIO (dùng lại
/// PDFtoImage, cùng convention 200dpi với OcrWorker.cs) rồi chạy SealSignatureDetector (C# thuần,
/// không AI) để tách vùng con dấu/chữ ký.
/// </summary>
public interface IOcrModuleSealSignatureService
{
    Task<SealSignatureRunResult> RunAsync(OcrModuleJob job);
}

public class OcrModuleSealSignatureService : IOcrModuleSealSignatureService
{
    private readonly IMinioStorageService _minioService;
    private readonly IOcrModuleRepository _repository;

    public OcrModuleSealSignatureService(IMinioStorageService minioService, IOcrModuleRepository repository)
    {
        _minioService = minioService;
        _repository = repository;
    }

    public async Task<SealSignatureRunResult> RunAsync(OcrModuleJob job)
    {
        using var fileStream = await _minioService.DownloadFileAsync(job.SourceBucket, job.SourceFilePath);
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        var pdfBytes = ms.ToArray();

        var pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
        var allRegions = await _repository.GetAllRegionEntitiesAsync(job.Id);
        var regionsByPage = allRegions.GroupBy(r => r.PageNumber).ToDictionary(g => g.Key, g => g.ToList());

        var newSealRegions = new List<OcrModuleRegion>();
        var signatureUpdates = new Dictionary<string, double>();

        for (var i = 0; i < pageCount; i++)
        {
            var pageNumber = i + 1;

            using var imgStream = new MemoryStream();
            var renderOptions = new PDFtoImage.RenderOptions { Dpi = 200, WithAnnotations = true };
            PDFtoImage.Conversion.SaveJpeg(imgStream, pdfBytes, password: null, page: i, options: renderOptions);
            var pageImageBytes = imgStream.ToArray();

            if (pageImageBytes.Length == 0) continue;

            var seals = SealSignatureDetector.DetectSeals(pageImageBytes, pageNumber);
            foreach (var seal in seals)
            {
                newSealRegions.Add(new OcrModuleRegion
                {
                    Id = UuidHelper.NewUuid(),
                    JobId = job.Id,
                    PageNumber = seal.PageNumber,
                    BoxX0 = seal.BoxX0,
                    BoxY0 = seal.BoxY0,
                    BoxX1 = seal.BoxX1,
                    BoxY1 = seal.BoxY1,
                    TextRaw = "(Con dấu — nhận diện theo màu mực đỏ)",
                    RegionType = "Seal",
                    SealSignatureScore = seal.Score,
                    Status = "Detected",
                });
            }

            if (regionsByPage.TryGetValue(pageNumber, out var pageRegions))
            {
                using var image = Image.Load<Rgba32>(pageImageBytes);
                var suggestions = SealSignatureDetector.SuggestSignatureRegions(pageRegions, image.Height);
                foreach (var kv in suggestions)
                {
                    signatureUpdates[kv.Key] = kv.Value;
                }
            }
        }

        if (newSealRegions.Count > 0)
        {
            await _repository.InsertRegionsAsync(newSealRegions);
        }

        if (signatureUpdates.Count > 0)
        {
            await _repository.UpdateRegionsAsSignatureAsync(signatureUpdates);
        }

        return new SealSignatureRunResult
        {
            SealCount = newSealRegions.Count,
            SignatureCount = signatureUpdates.Count,
        };
    }
}
