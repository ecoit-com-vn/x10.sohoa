using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.DigitizationService.Core.Services.OcrModule;

/// <summary>
/// Yêu cầu 92 — tổng hợp lỗi có cấu trúc theo Job, gom tín hiệu đã có sẵn từ các giai đoạn trước
/// (93 độ tin cậy thấp, 95 gợi ý chính tả bị từ chối, 90 sai khác mẫu bị đánh dấu, 88 vùng công thức
/// nghi ngờ, 94 con dấu/chữ ký điểm khớp thấp) — thuần C#, không gọi model AI.
/// </summary>
public interface IOcrModuleErrorAnalysisAggregator
{
    /// <summary>pageNumber = null → tổng hợp lại toàn bộ Job; có giá trị → chỉ tổng hợp lại đúng trang đó.</summary>
    Task<List<OcrModuleErrorAnalysis>> AggregateAsync(string jobId, int? pageNumber = null);
}

public class OcrModuleErrorAnalysisAggregator : IOcrModuleErrorAnalysisAggregator
{
    private const double LowConfidenceThreshold = 0.6;
    private const double LowSealSignatureScoreThreshold = 0.3;

    private readonly IOcrModuleRepository _repository;

    public OcrModuleErrorAnalysisAggregator(IOcrModuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<OcrModuleErrorAnalysis>> AggregateAsync(string jobId, int? pageNumber = null)
    {
        var regions = await _repository.GetAllRegionEntitiesAsync(jobId);
        var templateDiffs = await _repository.GetTemplateDiffResultsAsync(jobId);
        if (pageNumber.HasValue)
        {
            regions = regions.Where(r => r.PageNumber == pageNumber.Value).ToList();
            templateDiffs = templateDiffs.Where(d => d.PageNumber == pageNumber.Value).ToList();
        }

        var errors = new List<OcrModuleErrorAnalysis>();

        foreach (var region in regions)
        {
            // Region đã được người dùng hiệu chỉnh tay hoặc xác nhận thì không còn coi là lỗi độ tin cậy
            // thấp nữa, dù giá trị Confidence cũ (trước khi sửa) có thể vẫn còn thấp trong 1 số đường dữ liệu.
            var manuallyResolved = region.Status == "Edited" || region.Status == "Confirmed";

            if (!manuallyResolved && region.RegionType == "Text" && region.Confidence.HasValue && region.Confidence.Value < LowConfidenceThreshold)
            {
                errors.Add(NewError(jobId, region.Id, region.PageNumber, "LowConfidence",
                    region.Confidence.Value < 0.4 ? "High" : "Medium",
                    $"Độ tin cậy OCR thấp ({region.Confidence.Value:P0}): \"{Truncate(region.TextRaw)}\""));
            }

            if (region.RegionType == "Formula" && region.Confidence.HasValue && region.Confidence.Value < LowConfidenceThreshold)
            {
                errors.Add(NewError(jobId, region.Id, region.PageNumber, "FormulaHeuristicMismatch", "Medium",
                    $"Vùng công thức có độ tin cậy OCR thấp, cần kiểm tra lại: \"{Truncate(region.TextRaw)}\""));
            }

            if ((region.RegionType == "Seal" || region.RegionType == "Signature")
                && region.Status == "Detected"
                && GetSealSignatureScore(region) is double score && score < LowSealSignatureScoreThreshold)
            {
                errors.Add(NewError(jobId, region.Id, region.PageNumber, "SealSignatureLowScore", "Low",
                    $"Độ khớp heuristic thấp ({score:P0}) cho vùng {region.RegionType}."));
            }
        }

        var spellcheckRejected = await GetSpellcheckRejectedRegionsAsync(jobId, pageNumber);
        errors.AddRange(spellcheckRejected);

        foreach (var diff in templateDiffs.Where(d => d.Status == "Flagged"))
        {
            errors.Add(NewError(jobId, diff.RegionId, diff.PageNumber, "TemplateMismatch", "Medium", diff.Detail));
        }

        await _repository.ReplaceErrorAnalysisAsync(jobId, errors, pageNumber);

        // Trả về đầy đủ danh sách lỗi hiện có của Job (không chỉ trang vừa chạy) — các trang khác vẫn
        // giữ nguyên lỗi đã tổng hợp trước đó nên UI không bị "mất" dữ liệu khi người dùng chỉ chọn 1 trang.
        return await _repository.GetErrorAnalysisAsync(jobId);
    }

    private async Task<List<OcrModuleErrorAnalysis>> GetSpellcheckRejectedRegionsAsync(string jobId, int? pageNumber)
    {
        var regions = await _repository.GetAllRegionsAsync(jobId);
        return regions
            .Where(r => r.SpellcheckStatus == "Rejected" && (pageNumber == null || r.PageNumber == pageNumber.Value))
            .Select(r => NewError(jobId, r.Id, r.PageNumber, "SpellcheckRejected", "Low",
                $"Gợi ý chính tả bị từ chối: \"{Truncate(r.TextRaw)}\""))
            .ToList();
    }

    private static double? GetSealSignatureScore(OcrModuleRegion region) => region.SealSignatureScore;

    private static OcrModuleErrorAnalysis NewError(string jobId, string? regionId, int pageNumber, string category, string severity, string? detail) => new()
    {
        Id = UuidHelper.NewUuid(),
        JobId = jobId,
        RegionId = regionId,
        PageNumber = pageNumber,
        ErrorCategory = category,
        Severity = severity,
        Detail = detail,
        ResolvedStatus = "Open",
    };

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= 120 ? text : text[..120] + "...";
    }
}
