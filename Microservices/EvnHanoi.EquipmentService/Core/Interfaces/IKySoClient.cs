using EvnHanoi.EquipmentService.Core.DTOs.DigitalSignature;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

/// <summary>
/// Client gọi API "ký số" ngoài (gateway EVN Hanoi — 3 API: lấy serial number chứng thư số, lấy
/// ảnh chữ ký, ký PDF bằng ảnh chữ ký base64). Dùng HttpClient đặt tên "KySo" (đăng ký ở Program.cs
/// EquipmentService), KHÔNG dùng chung client "CA" scaffold sẵn trong EvnHanoi.SyncService (client
/// đó chưa được dùng ở đâu — xem comment ở Program.cs).
/// </summary>
public interface IKySoClient
{
    /// <summary>API 1: lay-thong-tin-serial-number — trả về null nếu không có dữ liệu (data rỗng).</summary>
    Task<KySoSerialNumberData?> GetSerialNumberAsync(long nsId, CancellationToken cancellationToken = default);

    /// <summary>API 2: lay-anh-chu-ky — trả về base64 PNG hoặc null nếu API báo lỗi/không có ảnh.</summary>
    Task<string?> GetSignatureImageAsync(long nsId, CancellationToken cancellationToken = default);

    /// <summary>
    /// API 3: sign-pdf-base64-image. LƯU Ý: response HTTP 200 + status/statusCode ở tầng ngoài chỉ
    /// nghĩa là gọi API thành công — kết quả ký THỰC SỰ nằm ở response.Data.Status.
    /// </summary>
    Task<KySoSignPdfResultData> SignPdfAsync(KySoSignPdfRequest request, CancellationToken cancellationToken = default);
}
