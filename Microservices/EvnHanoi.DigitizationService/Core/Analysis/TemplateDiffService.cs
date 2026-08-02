using EvnHanoi.DigitizationService.Models.OcrModule;

namespace EvnHanoi.DigitizationService.Core.Analysis;

/// <summary>
/// Yêu cầu 90 — so sánh vùng văn bản của 1 Job hiện tại với 1 mẫu tham chiếu (snapshot) bằng
/// heuristic vị trí (tỷ lệ chồng lấn khung — IoU) + độ tương đồng chuỗi (Levenshtein), thuần C#,
/// không dùng model AI.
/// </summary>
public static class TemplateDiffService
{
    private const double MinOverlapToMatch = 0.2;
    private const double PositionShiftOverlapThreshold = 0.6;
    private const double TextMismatchSimilarityThreshold = 0.7;
    private const int MaxDetailLength = 120;

    public static List<OcrModuleTemplateDiffResult> ComputeDiff(
        IReadOnlyList<OcrModuleRegion> currentRegions,
        IReadOnlyList<TemplateRegionSnapshot> referenceRegions)
    {
        var results = new List<OcrModuleTemplateDiffResult>();
        var matchedCurrentIds = new HashSet<string>();
        var textRegions = currentRegions.Where(r => r.RegionType == "Text").ToList();

        foreach (var refRegion in referenceRegions)
        {
            var samePage = textRegions.Where(r => r.PageNumber == refRegion.PageNumber).ToList();

            OcrModuleRegion? best = null;
            var bestOverlap = 0.0;
            foreach (var candidate in samePage)
            {
                var overlap = BoxOverlapRatio(candidate, refRegion);
                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    best = candidate;
                }
            }

            if (best == null || bestOverlap < MinOverlapToMatch)
            {
                results.Add(new OcrModuleTemplateDiffResult
                {
                    PageNumber = refRegion.PageNumber,
                    DiffType = "Missing",
                    Detail = $"Không tìm thấy vùng tương ứng với mẫu: \"{Truncate(refRegion.Text)}\"",
                });
                continue;
            }

            matchedCurrentIds.Add(best.Id);

            var similarity = TextSimilarity(best.TextRaw, refRegion.Text);
            if (similarity < TextMismatchSimilarityThreshold)
            {
                results.Add(new OcrModuleTemplateDiffResult
                {
                    RegionId = best.Id,
                    PageNumber = refRegion.PageNumber,
                    DiffType = "TextMismatch",
                    Detail = $"Mẫu: \"{Truncate(refRegion.Text)}\" — Hiện tại: \"{Truncate(best.TextRaw)}\"",
                });
            }
            else if (bestOverlap < PositionShiftOverlapThreshold)
            {
                results.Add(new OcrModuleTemplateDiffResult
                {
                    RegionId = best.Id,
                    PageNumber = refRegion.PageNumber,
                    DiffType = "PositionShift",
                    Detail = $"Vị trí vùng \"{Truncate(best.TextRaw)}\" lệch đáng kể so với mẫu.",
                });
            }
        }

        foreach (var region in textRegions)
        {
            if (!matchedCurrentIds.Contains(region.Id))
            {
                results.Add(new OcrModuleTemplateDiffResult
                {
                    RegionId = region.Id,
                    PageNumber = region.PageNumber,
                    DiffType = "Extra",
                    Detail = $"Vùng mới không có trong mẫu: \"{Truncate(region.TextRaw)}\"",
                });
            }
        }

        return results;
    }

    private static double BoxOverlapRatio(OcrModuleRegion a, TemplateRegionSnapshot b)
    {
        var x0 = Math.Max(a.BoxX0, b.BoxX0);
        var y0 = Math.Max(a.BoxY0, b.BoxY0);
        var x1 = Math.Min(a.BoxX1, b.BoxX1);
        var y1 = Math.Min(a.BoxY1, b.BoxY1);

        var interW = Math.Max(0, x1 - x0);
        var interH = Math.Max(0, y1 - y0);
        var interArea = interW * interH;
        if (interArea <= 0) return 0;

        var areaA = Math.Max(0, a.BoxX1 - a.BoxX0) * Math.Max(0, a.BoxY1 - a.BoxY0);
        var areaB = Math.Max(0, b.BoxX1 - b.BoxX0) * Math.Max(0, b.BoxY1 - b.BoxY0);
        var unionArea = areaA + areaB - interArea;

        return unionArea > 0 ? interArea / unionArea : 0;
    }

    private static double TextSimilarity(string a, string b)
    {
        a ??= string.Empty;
        b ??= string.Empty;
        if (a.Length == 0 && b.Length == 0) return 1.0;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }

    private static string Truncate(string text) =>
        text.Length <= MaxDetailLength ? text : text[..MaxDetailLength] + "...";
}
