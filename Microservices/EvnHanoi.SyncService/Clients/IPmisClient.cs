using EvnHanoi.SyncService.Models.Pmis;

namespace EvnHanoi.SyncService.Clients;

/// <summary>9 API pull PMIS theo tài liệu "[EVNHANOI_SHHSKT] Phương án đồng bộ PMIS", cộng thêm API ảnh QR
/// (phát hiện khi gọi thật vào gateway PMIS — xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md).</summary>
public interface IPmisClient
{
    Task<PmisListResponse<PmisSubstationDto>> GetSubstationsAsync(PmisSubstationSearchRequest request);
    Task<PmisListResponse<PmisLineDto>> GetLinesAsync(PmisLineSearchRequest request);
    Task<PmisListResponse<PmisDeviceTypeDto>> GetSubstationDeviceTypesAsync(PmisDeviceTypeSearchRequest request);
    Task<PmisListResponse<PmisSubstationDeviceDto>> GetSubstationDevicesAsync(PmisSubstationDeviceSearchRequest request);
    Task<PmisListResponse<PmisDeviceTypeDto>> GetLineDeviceTypesAsync(PmisDeviceTypeSearchRequest request);
    Task<PmisListResponse<PmisLineDeviceDto>> GetLineDevicesAsync(PmisLineDeviceSearchRequest request);
    Task<PmisDeviceDetailDto?> GetDeviceDetailAsync(PmisDeviceDetailRequest request);
    Task<PmisListResponse<PmisSubstationDocumentDto>> GetSubstationDocumentsAsync(PmisSubstationDocumentSearchRequest request);
    Task<PmisListResponse<PmisLineDocumentDto>> GetLineDocumentsAsync(PmisLineDocumentSearchRequest request);

    /// <summary>Tải ảnh QR nhị phân thật (JPEG) theo mã thiết bị — trả null nếu API chưa cấu hình hoặc lỗi
    /// (không chặn phần còn lại của đồng bộ). Field maQRCode ở các API khác chỉ là URL, không phải base64.</summary>
    Task<byte[]?> GetDeviceQrImageBytesAsync(string idPmis);

    /// <summary>Tải file nhị phân tài liệu đính kèm theo URL động (field "File" của API 8/9) — trả null
    /// nếu lỗi, không throw (đồng bộ tài liệu không được chặn lượt đồng bộ chính).</summary>
    Task<byte[]?> DownloadDocumentFileAsync(string fileUrl);
}

/// <summary>Báo lỗi nghiệp vụ khi 1 API PMIS chưa được cấu hình (chưa bật hoặc chưa nhập Url) qua màn "Cấu hình kết nối PMIS".</summary>
public class PmisEndpointNotConfiguredException(string apiCode, string displayName)
    : Exception($"API PMIS '{displayName}' ({apiCode}) chưa được cấu hình hoặc đang tắt. Vào Quản trị hệ thống > Cấu hình kết nối PMIS để thiết lập.")
{
    public string ApiCode { get; } = apiCode;
}
