namespace EvnHanoi.DigitizationService.Services.OcrModule;

/// <summary>
/// DPI dùng khi render trang PDF ra ảnh JPEG cho Module OCR. Hai hằng số này PHẢI khớp với đúng
/// DPI đã dùng để sinh toạ độ box của từng loại region — nếu ảnh hiển thị cho FE (DisplayDpi) và
/// ảnh dùng để tính box (OcrSourceDpi cho Text, hoặc DisplayDpi cho Seal/Signature) lệch DPI nhau,
/// box sẽ bị vẽ sai vị trí (đã xảy ra: OcrWorker từng trôi xuống 150 trong khi GetPageImage ở 200).
/// </summary>
public static class OcrModuleImageDpi
{
    /// <summary>DPI OcrWorker dùng để render trang PDF trước khi gửi OCR — toạ độ box của region loại Text là pixel tuyệt đối theo DPI này.</summary>
    public const int OcrSourceDpi = 150;

    /// <summary>DPI dùng để render ảnh hiển thị cho người dùng (OcrModuleJobController.GetPageImage) và để tính box Con dấu/Chữ ký (OcrModuleSealSignatureService).</summary>
    public const int DisplayDpi = 200;

    /// <summary>Hệ số quy đổi toạ độ box từ hệ pixel OcrSourceDpi sang hệ pixel DisplayDpi.</summary>
    public const double SourceToDisplayScale = (double)DisplayDpi / OcrSourceDpi;
}
