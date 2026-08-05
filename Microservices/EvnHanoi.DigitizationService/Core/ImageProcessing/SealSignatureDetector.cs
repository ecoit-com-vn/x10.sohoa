using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EvnHanoi.DigitizationService.Core.ImageProcessing;

public class InkRegionCandidate
{
    public int PageNumber { get; set; }
    public double BoxX0 { get; set; }
    public double BoxY0 { get; set; }
    public double BoxX1 { get; set; }
    public double BoxY1 { get; set; }
    public double Score { get; set; }
}

/// <summary>
/// Yêu cầu 94 — ĐỀ XUẤT ứng viên con dấu VÀ chữ ký bằng xử lý ảnh cổ điển thuần C# (SixLabors.ImageSharp),
/// KHÔNG dùng model AI ở bước này VÀ KHÔNG dựa vào box của vùng văn bản OCR (box OCR nhắm vào chữ đọc
/// được, không khớp hình dạng/kích thước thật của nét chữ ký tay — vd. OCR chỉ đọc được 1-2 ký tự rời rạc
/// trong khi nét chữ ký trải dài cả trăm pixel). Bước này quét trực tiếp pixel để tìm CẢ 2 loại khối mực:
/// khối gần vuông/tròn, đặc, kích thước ~2-4cm (ứng viên con dấu) và khối dẹt ngang, thưa hơn, kích thước
/// đa dạng hơn (ứng viên chữ ký) — ưu tiên recall (chấp nhận nhiều dương tính giả), quyết định cuối cùng
/// "có đúng là con dấu/chữ ký không, loại gì" do bước xác thực bằng LLM vision ở
/// <see cref="Services.OcrModule.OcrModuleSealSignatureService"/> đảm nhiệm.
/// </summary>
public static class SealSignatureDetector
{
    // Chữ ký tay thường chỉ dày 1-3px ở 200dpi — lấy mẫu thưa hơn 1px sẽ bỏ sót phần lớn nét chữ ký (đã
    // xác nhận qua test thực tế: SampleStep=3 cho 0 ứng viên chữ ký trên trang có chữ ký thật, SampleStep=1
    // tìm đúng cả 2). Con dấu vẫn nhận diện tốt ở mức lấy mẫu này (nét khắc dấu dày hơn nhiều).
    private const int SampleStep = 1;
    private const int MaxCandidatesPerPage = 12; // chặn số ứng viên tối đa/trang để bước xác thực LLM vision không phải xử lý quá nhiều ảnh trong 1 lần gọi

    // --- Ứng viên con dấu: khối gần vuông/tròn, đặc, khoảng cách giữa các nét mực nhỏ (hoa văn khắc dấu) ---
    private const int SealDilateRadiusGrid = 10; // px thật (SampleStep=1) — gộp các nét mực rời rạc của CÙNG 1 con dấu (khoảng trắng giữa các ký tự khắc trên dấu) thành 1 khối duy nhất; giá trị này kiểm chứng thực nghiệm trên tài liệu thật cho khung khớp con dấu chính xác nhất (bán kính lớn hơn merge lấn cả chữ ký/văn bản lân cận, nhỏ hơn thì vỡ dấu thành nhiều mảnh)
    private const int MinInkCellCount = 24; // số điểm ảnh mực THẬT (trước khi nở) tối thiểu trong khối — loại nhiễu điểm ảnh lẻ bị nở to ra (đã tăng tương ứng với SampleStep=1, mật độ điểm mẫu cao hơn 9 lần so với SampleStep=3)
    private const int MinBlobSizePx = 20;
    private const int MaxColorSealSizePx = 650; // ngay cả mực đỏ/xanh thật cũng phải khớp kích thước vật lý con dấu (nới hơn kích thước 1 con dấu "sạch" vì trên thực tế dấu thường bị chữ ký/văn bản lân cận chồng lấn, làm khối gộp lớn hơn) — vẫn đủ chặt để loại 1 đoạn văn bản/logo màu đơn lẻ
    private const double MinSealAspectRatio = 0.3;
    private const double MaxSealAspectRatio = 3.0;
    private const double MinSealDensity = 0.04;

    // Khối chỉ có mực đen/xám (không có mực đỏ/xanh thật) rất khó phân biệt với 1 đoạn văn bản in bằng
    // heuristic thuần — phải xiết chặt thêm bằng kích thước vật lý thật của con dấu (~2-4cm ở 200dpi)
    // và tỉ lệ khung gần vuông/tròn, tránh cả 1 đoạn văn bản bị coi là ứng viên con dấu.
    private const int MinGraySealDiameterPx = 110;
    private const int MaxGraySealDiameterPx = 420;
    private const double MinGrayAspectRatio = 0.6;
    private const double MaxGrayAspectRatio = 1.6;
    private const double MinGrayDensity = 0.08;

    // --- Ứng viên chữ ký: khối dẹt ngang (rộng hơn cao rõ rệt), thưa hơn con dấu, khoảng cách giữa các nét
    // chữ/cụm chữ trong 1 chữ ký tay thường lớn hơn khoảng cách giữa các nét khắc trên 1 con dấu, nên cần
    // bán kính nở lớn hơn để gộp đúng thành 1 khối duy nhất thay vì vỡ theo từng cụm nét rời.
    private const int SignatureDilateRadiusGrid = 12; // px thật (SampleStep=1) — kiểm chứng thực nghiệm: bán kính lớn hơn dễ merge lấn sang chữ/logo in lân cận, nhỏ hơn dễ vỡ chữ ký thành nhiều mảnh rời
    private const int MinSignatureWidthPx = 60;
    private const int MaxSignatureWidthPx = 700;
    private const int MinSignatureHeightPx = 15;
    private const int MaxSignatureHeightPx = 220;
    private const double MinSignatureAspectRatio = 1.1; // phải rõ ràng rộng hơn cao — phân biệt với con dấu (gần vuông/tròn)
    private const double MaxSignatureAspectRatio = 10.0; // vẫn thấp hơn hẳn 1 dòng chữ in đầy chiều ngang trang (thường >20)
    private const double MinSignatureDensity = 0.015; // nét chữ ký mảnh, thưa hơn nhiều so với khối khắc dấu
    private const double MaxSignatureDensity = 0.55; // loại trừ khối mực đặc kín (bảng kẻ, ô tô đen...)

    /// <summary>Quét ảnh 1 trang để tìm ứng viên vùng con dấu (mực đỏ/xanh, hoặc mảng đen/xám đậm nếu là bản photo đen trắng).</summary>
    public static List<InkRegionCandidate> DetectSealCandidates(byte[] pageImageBytes, int pageNumber)
    {
        using var image = Image.Load<Rgba32>(pageImageBytes);
        var (isColorInk, isAnyInk, gridW, gridH) = BuildInkGrids(image);
        var dilated = Dilate(isAnyInk, gridW, gridH, SealDilateRadiusGrid);
        var visited = new bool[gridW, gridH];
        var candidates = new List<InkRegionCandidate>();

        for (var gx = 0; gx < gridW; gx++)
        {
            for (var gy = 0; gy < gridH; gy++)
            {
                if (!dilated[gx, gy] || visited[gx, gy]) continue;

                var (minX, minY, maxX, maxY) = FloodFill(dilated, visited, gx, gy, gridW, gridH);
                var blobW = maxX - minX + 1;
                var blobH = maxY - minY + 1;
                var aspect = (double)blobW / blobH;

                var colorInkCount = CountTrue(isColorInk, minX, minY, maxX, maxY);
                var hasRealColor = colorInkCount >= MinInkCellCount;

                double density;
                if (hasRealColor)
                {
                    // Mực đỏ/xanh thật — đặc trưng màu giúp loại trừ văn bản in thường, nhưng vẫn phải khớp
                    // kích thước vật lý con dấu (không chỉ có sàn tối thiểu) — nếu không, 1 cụm chữ/logo màu
                    // (vd. tiêu đề màu xanh) lọt qua tỉ lệ khung sẽ bị coi nhầm là con dấu.
                    var blobWPx = blobW * SampleStep;
                    var blobHPx = blobH * SampleStep;
                    if (blobWPx < MinBlobSizePx || blobWPx > MaxColorSealSizePx) continue;
                    if (blobHPx < MinBlobSizePx || blobHPx > MaxColorSealSizePx) continue;
                    if (aspect < MinSealAspectRatio || aspect > MaxSealAspectRatio) continue;

                    density = (double)colorInkCount / (blobW * blobH);
                    if (density < MinSealDensity) continue;
                }
                else
                {
                    // Chỉ có mực đen/xám — PHẢI xiết theo kích thước vật lý con dấu thật + tỉ lệ gần vuông/tròn,
                    // nếu không sẽ bắt nhầm cả đoạn văn bản in thường thành ứng viên con dấu.
                    var blobWPx = blobW * SampleStep;
                    var blobHPx = blobH * SampleStep;
                    if (blobWPx < MinGraySealDiameterPx || blobWPx > MaxGraySealDiameterPx) continue;
                    if (blobHPx < MinGraySealDiameterPx || blobHPx > MaxGraySealDiameterPx) continue;
                    if (aspect < MinGrayAspectRatio || aspect > MaxGrayAspectRatio) continue;

                    var inkCount = CountTrue(isAnyInk, minX, minY, maxX, maxY);
                    density = (double)inkCount / (blobW * blobH);
                    if (density < MinGrayDensity) continue;
                }

                candidates.Add(ToCandidate(pageNumber, minX, minY, maxX, maxY, image.Width, image.Height, density));
            }
        }

        return candidates.OrderByDescending(c => c.Score).Take(MaxCandidatesPerPage).ToList();
    }

    /// <summary>
    /// Quét ảnh 1 trang để tìm ứng viên vùng chữ ký — hoàn toàn dựa trên hình dạng khối mực thật trên ảnh
    /// (khối dẹt ngang, thưa), KHÔNG dùng box của vùng văn bản OCR (OCR thường chỉ đọc được rời rạc vài ký
    /// tự trong nét chữ ký, cho box sai kích thước/vị trí so với nét chữ ký thật).
    /// </summary>
    public static List<InkRegionCandidate> DetectSignatureCandidates(byte[] pageImageBytes, int pageNumber)
    {
        using var image = Image.Load<Rgba32>(pageImageBytes);
        var (_, isAnyInk, gridW, gridH) = BuildInkGrids(image);
        var dilated = Dilate(isAnyInk, gridW, gridH, SignatureDilateRadiusGrid);
        var visited = new bool[gridW, gridH];
        var candidates = new List<InkRegionCandidate>();

        for (var gx = 0; gx < gridW; gx++)
        {
            for (var gy = 0; gy < gridH; gy++)
            {
                if (!dilated[gx, gy] || visited[gx, gy]) continue;

                var (minX, minY, maxX, maxY) = FloodFill(dilated, visited, gx, gy, gridW, gridH);
                var blobW = maxX - minX + 1;
                var blobH = maxY - minY + 1;
                var blobWPx = blobW * SampleStep;
                var blobHPx = blobH * SampleStep;
                var aspect = (double)blobW / blobH;

                if (blobWPx < MinSignatureWidthPx || blobWPx > MaxSignatureWidthPx) continue;
                if (blobHPx < MinSignatureHeightPx || blobHPx > MaxSignatureHeightPx) continue;
                if (aspect < MinSignatureAspectRatio || aspect > MaxSignatureAspectRatio) continue;

                var inkCount = CountTrue(isAnyInk, minX, minY, maxX, maxY);
                var density = (double)inkCount / (blobW * blobH);
                if (density < MinSignatureDensity || density > MaxSignatureDensity) continue;

                candidates.Add(ToCandidate(pageNumber, minX, minY, maxX, maxY, image.Width, image.Height, density));
            }
        }

        return candidates.OrderByDescending(c => c.Score).Take(MaxCandidatesPerPage).ToList();
    }

    private static InkRegionCandidate ToCandidate(
        int pageNumber, int minX, int minY, int maxX, int maxY, int imageWidth, int imageHeight, double density) => new()
    {
        PageNumber = pageNumber,
        BoxX0 = minX * SampleStep,
        BoxY0 = minY * SampleStep,
        BoxX1 = Math.Min((maxX + 1) * SampleStep, imageWidth),
        BoxY1 = Math.Min((maxY + 1) * SampleStep, imageHeight),
        Score = Math.Min(1.0, density),
    };

    private static (bool[,] isColorInk, bool[,] isAnyInk, int gridW, int gridH) BuildInkGrids(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var gridW = (width / SampleStep) + 1;
        var gridH = (height / SampleStep) + 1;
        var isColorInk = new bool[gridW, gridH]; // mực đỏ/xanh — khó nhầm với văn bản in thường
        var isAnyInk = new bool[gridW, gridH]; // isColorInk + mực đen/xám đậm (bản photo đen trắng hoặc mực đen/xanh đậm)

        for (var y = 0; y < height; y += SampleStep)
        {
            var row = image.GetPixelRowSpan(y);
            var gy = y / SampleStep;
            for (var x = 0; x < width; x += SampleStep)
            {
                var p = row[x];
                var gx = x / SampleStep;
                if (IsColorInk(p.R, p.G, p.B))
                {
                    isColorInk[gx, gy] = true;
                    isAnyInk[gx, gy] = true;
                }
                else if (IsDarkInk(p.R, p.G, p.B))
                {
                    isAnyInk[gx, gy] = true;
                }
            }
        }

        return (isColorInk, isAnyInk, gridW, gridH);
    }

    private static bool IsColorInk(byte r, byte g, byte b)
    {
        // Mực đỏ: kênh đỏ chiếm ưu thế rõ rệt so với xanh lá/xanh dương.
        var isRed = r > 110 && (r - g) > 35 && (r - b) > 35;
        // Mực xanh (dương/lam): kênh xanh dương chiếm ưu thế, không quá sáng (không phải nền trắng/xám nhạt).
        var isBlue = b > 90 && (b - r) > 25 && (b - g) > 15;
        return isRed || isBlue;
    }

    private static bool IsDarkInk(byte r, byte g, byte b)
    {
        // Mảng mực đen/xám đậm: bản photo đen trắng làm con dấu mất màu gốc, hoặc mực bút đen/xanh đậm của
        // chữ ký tay — phân biệt với nền giấy (luôn rất sáng) bằng ngưỡng độ sáng trung bình thấp. Riêng
        // nhóm này còn bị xiết thêm theo kích thước/tỉ lệ vật lý ở từng hàm Detect* vì không đủ đặc trưng
        // để tự phân biệt với văn bản in thường chỉ bằng màu sắc.
        var luminance = (r + g + b) / 3;
        return luminance < 90;
    }

    private static int CountTrue(bool[,] grid, int minX, int minY, int maxX, int maxY)
    {
        var count = 0;
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (grid[x, y]) count++;
            }
        }

        return count;
    }

    private static bool[,] Dilate(bool[,] grid, int gridW, int gridH, int radius)
    {
        // Nở theo 2 chiều tách biệt (ngang rồi dọc) — O(n*radius) thay vì O(n*radius^2) nếu nở theo ô vuông trực tiếp.
        var horizontal = new bool[gridW, gridH];
        for (var y = 0; y < gridH; y++)
        {
            for (var x = 0; x < gridW; x++)
            {
                if (!grid[x, y]) continue;
                var xs = Math.Max(0, x - radius);
                var xe = Math.Min(gridW - 1, x + radius);
                for (var xx = xs; xx <= xe; xx++) horizontal[xx, y] = true;
            }
        }

        var result = new bool[gridW, gridH];
        for (var x = 0; x < gridW; x++)
        {
            for (var y = 0; y < gridH; y++)
            {
                if (!horizontal[x, y]) continue;
                var ys = Math.Max(0, y - radius);
                var ye = Math.Min(gridH - 1, y + radius);
                for (var yy = ys; yy <= ye; yy++) result[x, yy] = true;
            }
        }

        return result;
    }

    private static (int minX, int minY, int maxX, int maxY) FloodFill(
        bool[,] grid, bool[,] visited, int startX, int startY, int gridW, int gridH)
    {
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));
        visited[startX, startY] = true;

        int minX = startX, maxX = startX, minY = startY, maxY = startY;

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
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

        return (minX, minY, maxX, maxY);
    }

    private static readonly (int, int)[] Neighbors =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
    };
}
