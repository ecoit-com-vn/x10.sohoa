using EvnHanoi.DigitizationService.Models.OcrModule;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EvnHanoi.DigitizationService.Core.ImageProcessing;

public class SealCandidate
{
    public int PageNumber { get; set; }
    public double BoxX0 { get; set; }
    public double BoxY0 { get; set; }
    public double BoxX1 { get; set; }
    public double BoxY1 { get; set; }
    public double Score { get; set; }
}

/// <summary>
/// Yêu cầu 94 — nhận diện con dấu (vùng mực đỏ hình tròn/vuông) bằng xử lý ảnh cổ điển thuần C#
/// (SixLabors.ImageSharp), KHÔNG dùng model AI. Nhận diện chữ ký dựa trên heuristic vị trí + độ tin
/// cậy OCR bất thường trên các vùng văn bản đã có sẵn (không cần xử lý ảnh thêm). Điểm số trả về là
/// "độ khớp heuristic" tự tính, không phải confidence của model AI thật.
/// </summary>
public static class SealSignatureDetector
{
    private const int SampleStep = 3; // lấy mẫu mỗi 3px để giảm chi phí xử lý
    private const int MinBlobSizePx = 20; // kích thước tối thiểu (theo lưới đã downsample) của 1 cụm màu đỏ
    private const double MinAspectRatio = 0.5;
    private const double MaxAspectRatio = 2.0;

    /// <summary>Quét ảnh 1 trang (đã render sẵn, cùng hệ toạ độ pixel với box OCR) để tìm vùng con dấu màu đỏ.</summary>
    public static List<SealCandidate> DetectSeals(byte[] pageImageBytes, int pageNumber)
    {
        using var image = Image.Load<Rgba32>(pageImageBytes);
        var width = image.Width;
        var height = image.Height;

        var gridW = (width / SampleStep) + 1;
        var gridH = (height / SampleStep) + 1;
        var isRed = new bool[gridW, gridH];

        for (var y = 0; y < height; y += SampleStep)
        {
            var row = image.GetPixelRowSpan(y);
            var gy = y / SampleStep;
            for (var x = 0; x < width; x += SampleStep)
            {
                var p = row[x];
                if (IsRedInk(p.R, p.G, p.B))
                {
                    isRed[x / SampleStep, gy] = true;
                }
            }
        }

        var visited = new bool[gridW, gridH];
        var candidates = new List<SealCandidate>();

        for (var gx = 0; gx < gridW; gx++)
        {
            for (var gy = 0; gy < gridH; gy++)
            {
                if (!isRed[gx, gy] || visited[gx, gy]) continue;

                var (minX, minY, maxX, maxY, count) = FloodFill(isRed, visited, gx, gy, gridW, gridH);
                var blobW = maxX - minX + 1;
                var blobH = maxY - minY + 1;

                if (blobW < MinBlobSizePx / SampleStep || blobH < MinBlobSizePx / SampleStep) continue;

                var aspect = (double)blobW / blobH;
                if (aspect < MinAspectRatio || aspect > MaxAspectRatio) continue;

                var density = (double)count / (blobW * blobH);
                if (density < 0.15) continue; // quá thưa — có thể là nhiễu, không phải khối con dấu đặc

                candidates.Add(new SealCandidate
                {
                    PageNumber = pageNumber,
                    BoxX0 = minX * SampleStep,
                    BoxY0 = minY * SampleStep,
                    BoxX1 = Math.Min((maxX + 1) * SampleStep, width),
                    BoxY1 = Math.Min((maxY + 1) * SampleStep, height),
                    Score = Math.Min(1.0, density),
                });
            }
        }

        return candidates;
    }

    /// <summary>
    /// Gợi ý vùng chữ ký dựa trên vùng văn bản đã có: confidence thấp bất thường + nằm ở phần dưới
    /// trang (nơi thường đặt chữ ký) — không cần xử lý ảnh thêm.
    /// </summary>
    public static IReadOnlyDictionary<string, double> SuggestSignatureRegions(
        IReadOnlyList<OcrModuleRegion> pageRegions, double pageHeight)
    {
        var result = new Dictionary<string, double>();
        if (pageHeight <= 0) return result;

        const double bottomZoneRatio = 0.75;
        const double lowConfidenceThreshold = 0.6;

        foreach (var region in pageRegions)
        {
            if (region.RegionType != "Text") continue;

            var inBottomZone = region.BoxY0 >= pageHeight * bottomZoneRatio;
            var lowConfidence = region.Confidence.HasValue && region.Confidence.Value < lowConfidenceThreshold;

            if (inBottomZone && lowConfidence)
            {
                // Điểm khớp = mức độ thấp của confidence (càng thấp càng giống nét chữ ký tay không đều)
                result[region.Id] = Math.Round(1.0 - region.Confidence!.Value, 4);
            }
        }

        return result;
    }

    private static bool IsRedInk(byte r, byte g, byte b)
    {
        // Mực đỏ con dấu: kênh đỏ chiếm ưu thế rõ rệt so với xanh lá/xanh dương, không quá sáng (không phải nền trắng).
        return r > 110 && (r - g) > 35 && (r - b) > 35;
    }

    private static (int minX, int minY, int maxX, int maxY, int count) FloodFill(
        bool[,] grid, bool[,] visited, int startX, int startY, int gridW, int gridH)
    {
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));
        visited[startX, startY] = true;

        int minX = startX, maxX = startX, minY = startY, maxY = startY, count = 0;

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            count++;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;

            foreach (var (dx, dy) in Neighbors)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= gridW || ny >= gridH) continue;
                if (visited[nx, ny] || !grid[nx, ny]) continue;

                visited[nx, ny] = true;
                stack.Push((nx, ny));
            }
        }

        return (minX, minY, maxX, maxY, count);
    }

    private static readonly (int, int)[] Neighbors =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
    };
}
