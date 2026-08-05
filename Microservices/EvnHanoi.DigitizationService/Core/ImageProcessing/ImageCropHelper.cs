using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace EvnHanoi.DigitizationService.Core.ImageProcessing;

/// <summary>
/// Cắt 1 vùng con dấu/chữ ký nghi vấn (theo box pixel cùng hệ tọa độ với ảnh trang đã render 200dpi)
/// thành JPEG base64 để gửi kèm request multimodal cho bước xác thực bằng LLM server — yêu cầu 94.
/// </summary>
public static class ImageCropHelper
{
    private const int PaddingPx = 12;
    private const int JpegQuality = 85;

    public static string CropToBase64Jpeg(Image<Rgba32> pageImage, double boxX0, double boxY0, double boxX1, double boxY1)
    {
        // Kẹp chặt theo cả 2 chiều (không chỉ từng cạnh riêng lẻ) — box nguồn có thể không hợp lệ
        // (X0 > X1, tọa độ âm/vượt biên do dữ liệu OCR lỗi) nên phải đảm bảo rect luôn nằm trong ảnh,
        // tránh ImageSharp ném exception khi Crop ra ngoài biên.
        var rawX0 = Math.Min(boxX0, boxX1);
        var rawX1 = Math.Max(boxX0, boxX1);
        var rawY0 = Math.Min(boxY0, boxY1);
        var rawY1 = Math.Max(boxY0, boxY1);

        var x0 = Math.Clamp((int)rawX0 - PaddingPx, 0, Math.Max(0, pageImage.Width - 1));
        var y0 = Math.Clamp((int)rawY0 - PaddingPx, 0, Math.Max(0, pageImage.Height - 1));
        var x1 = Math.Clamp((int)rawX1 + PaddingPx, x0 + 1, pageImage.Width);
        var y1 = Math.Clamp((int)rawY1 + PaddingPx, y0 + 1, pageImage.Height);
        var width = x1 - x0;
        var height = y1 - y0;

        using var cropped = pageImage.Clone(ctx => ctx.Crop(new Rectangle(x0, y0, width, height)));
        using var ms = new MemoryStream();
        cropped.SaveAsJpeg(ms, new JpegEncoder { Quality = JpegQuality });
        return Convert.ToBase64String(ms.ToArray());
    }
}
