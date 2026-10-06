namespace EvnHanoi.DocumentProcessing;

/// <summary>
/// Chuyển ảnh → PDF thất bại (ảnh hỏng, vượt ngưỡng an toàn bộ nhớ/số frame...). Khác với lỗi nén thông
/// thường (được nuốt rồi fallback lưu file gốc), lỗi này PHẢI chặn upload (kế thừa InvalidOperationException để các controller upload hiện có tự trả 400 + Message): lưu ảnh gốc sẽ tái tạo đúng
/// lỗi "OCR không đọc được file ảnh". <see cref="Exception.Message"/> là thông điệp tiếng Việt hiển thị cho người dùng.
/// </summary>
public sealed class DocumentConversionException : InvalidOperationException
{
    public DocumentConversionException(string message, Exception? inner = null) : base(message, inner) { }
}
