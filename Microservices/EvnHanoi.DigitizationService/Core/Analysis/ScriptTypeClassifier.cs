using EvnHanoi.DigitizationService.Models.OcrModule;

namespace EvnHanoi.DigitizationService.Core.Analysis;

/// <summary>
/// Yêu cầu 93 — phân loại Printed/Handwritten/Mixed bằng heuristic thống kê thuần C#, KHÔNG gọi
/// model AI ngoài. Dựa trên 2 tín hiệu đã có sẵn từ ocr_vl_server cho mỗi vùng: độ lệch chiều cao
/// box so với trung bình cả trang (chữ in rất đều hàng, chữ viết tay lệch nhiều), và độ tin cậy OCR
/// (thường thấp và biến động hơn với chữ viết tay).
/// </summary>
public static class ScriptTypeClassifier
{
    private const double HeightDeviationThreshold = 0.35;
    private const double LowConfidenceThreshold = 0.80;

    public static IReadOnlyDictionary<string, string> ClassifyRegions(IReadOnlyList<OcrModuleRegion> regions)
    {
        var result = new Dictionary<string, string>();

        foreach (var pageGroup in regions.GroupBy(r => r.PageNumber))
        {
            var pageRegions = pageGroup.ToList();
            var heights = pageRegions.Select(r => Math.Max(r.BoxY1 - r.BoxY0, 0.01)).ToList();
            var avgHeight = heights.Average();

            foreach (var region in pageRegions)
            {
                var height = Math.Max(region.BoxY1 - region.BoxY0, 0.01);
                var heightDeviation = avgHeight > 0 ? Math.Abs(height - avgHeight) / avgHeight : 0;
                var lowConfidence = region.Confidence.HasValue && region.Confidence.Value < LowConfidenceThreshold;
                var irregularHeight = heightDeviation > HeightDeviationThreshold;

                string scriptType;
                if (lowConfidence && irregularHeight)
                {
                    scriptType = "Handwritten";
                }
                else if (lowConfidence || irregularHeight)
                {
                    scriptType = "Mixed";
                }
                else
                {
                    scriptType = "Printed";
                }

                result[region.Id] = scriptType;
            }
        }

        return result;
    }
}
