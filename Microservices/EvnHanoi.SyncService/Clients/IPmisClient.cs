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

    /// <summary>Tải file nhị phân tài liệu đính kèm theo URL động (field "File" của API 8/9) — trả
    /// Bytes=null nếu lỗi (không throw, đồng bộ tài liệu không được chặn lượt đồng bộ chính), kèm
    /// ErrorReason (rút gọn qua SyncErrorFormatter — không lộ stack trace) để caller lưu lại làm bằng
    /// chứng debug thay vì chỉ có trong log Serilog của pod (trước đây lỗi bị bỏ hẳn, EquipmentService
    /// chỉ thấy "file rỗng" mà không biết vì sao). <paramref name="endpointApiCode"/> chỉ định lấy header
    /// xác thực từ đúng cấu hình endpoint nguồn (SUBSTATION_DOCUMENT_LIST hoặc LINE_DOCUMENT_LIST) —
    /// không dùng cố định 1 endpoint cho cả 2 nguồn.</summary>
    Task<(byte[]? Bytes, string? ErrorReason)> DownloadDocumentFileAsync(string fileUrl, string endpointApiCode);

    /// <summary>Gọi API danh sách tài liệu (8/9) và trả NGUYÊN VĂN body JSON PMIS (không qua DTO) — chỉ để chẩn đoán
    /// khi nghi PMIS đổi tên/định dạng trường "File" (hệ thống không lưu phản hồi thô ở đâu khác).</summary>
    Task<string> PeekDocumentsRawAsync(bool isSubstation, string ownerPmisCode, int take);

    /// <summary>Gọi API tải file tài liệu (TaiFileTaiLieu?maTaiLieu=...) đúng như job tải file (gateway + header cấu hình)
    /// nhưng CHỈ trả thông tin chẩn đoán (mã HTTP, loại nội dung, kích thước, phần đầu) — không lưu file.</summary>
    /// <param name="method">GET hoặc POST.</param>
    /// <param name="bodyMode">Với POST: "query" (maTaiLieu trên query, không body), "json" (body {"maTaiLieu":...}),
    /// "form" (application/x-www-form-urlencoded).</param>
    /// <param name="path">Đường dẫn tương đối dưới gateway (mặc định /api/PmisDongBo/TaiFileTaiLieu) — để thử biến thể.</param>
    Task<DocumentFileProbe> ProbeDocumentFileAsync(string maTaiLieu, string endpointApiCode,
        string method = "GET", string bodyMode = "query", string path = "/api/PmisDongBo/TaiFileTaiLieu");

    /// <summary>Gọi BẤT KỲ API nào dưới gateway PMIS đã cấu hình và trả thông tin chẩn đoán (mã HTTP, header phản hồi,
    /// định dạng, phần đầu nội dung) — không lưu gì, không sửa DB. Dùng để thử nhanh các endpoint/phương thức.</summary>
    Task<DocumentFileProbe> ProbeApiAsync(PmisProbeRequest request);
}

/// <summary>Yêu cầu chẩn đoán tới 1 API PMIS bất kỳ (xem IPmisClient.ProbeApiAsync).</summary>
public class PmisProbeRequest
{
    /// <summary>GET | POST | HEAD | OPTIONS (không cho PUT/DELETE/PATCH — tránh sửa dữ liệu PMIS).</summary>
    public string Method { get; set; } = "GET";

    /// <summary>Đường dẫn tương đối dưới gateway, bắt đầu bằng /api/ (vd /api/PmisDongBo/TaiFileTaiLieu).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>URL ĐẦY ĐỦ (http/https) của endpoint tuỳ ý cần thử — thay cho Path. Chỉ gọi được tới host nằm trong danh sách
    /// cho phép: gateway PMIS đã cấu hình, host của endpoint HeadersFromApiCode, và "DebugSql:ProbeAllowedHosts" (phân tách
    /// bằng dấu phẩy, dạng host hoặc host:port). Header xác thực của PMIS CHỈ gửi kèm khi host đích là gateway/endpoint cấu hình
    /// — host khác (nếu được cho phép) không nhận header cấu hình, chỉ nhận <see cref="Headers"/> do caller truyền.</summary>
    public string? Url { get; set; }

    /// <summary>Do controller nạp từ cấu hình, KHÔNG nhận từ body.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> ExtraAllowedAuthorities { get; set; } = [];

    public Dictionary<string, string>? Query { get; set; }

    /// <summary>Body JSON (POST). Đối tượng/mảng/giá trị JSON bất kỳ.</summary>
    public System.Text.Json.JsonElement? JsonBody { get; set; }

    /// <summary>Body form-urlencoded (POST) — dùng thay JsonBody.</summary>
    public Dictionary<string, string>? FormBody { get; set; }

    /// <summary>Header bổ sung (ghi đè header cấu hình cùng tên).</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>Mã endpoint cấu hình lấy header xác thực + gateway (mặc định LINE_DOCUMENT_LIST).</summary>
    public string HeadersFromApiCode { get; set; } = "LINE_DOCUMENT_LIST";

    /// <summary>Số ký tự đầu của nội dung trả về (mặc định 300, tối đa 4000).</summary>
    public int HeadChars { get; set; } = 300;
}

/// <summary>Kết quả chẩn đoán 1 lần gọi API PMIS.</summary>
public class DocumentFileProbe
{
    public string? Method { get; set; }
    public Dictionary<string, string>? ResponseHeaders { get; set; }
    public string RequestUrl { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public long BodyBytes { get; set; }

    /// <summary>true nếu body lớn hơn giới hạn đọc (2 MB) — BodyBytes chỉ là phần đã đọc.</summary>
    public bool BodyTruncated { get; set; }
    public string? HeadHex { get; set; }
    public string? HeadText { get; set; }
    public string? DetectedFormat { get; set; }
    public List<string>? JsonKeys { get; set; }
    public string? Error { get; set; }
}

/// <summary>Báo lỗi nghiệp vụ khi 1 API PMIS chưa được cấu hình (chưa bật hoặc chưa nhập Url) qua màn "Cấu hình kết nối PMIS".</summary>
public class PmisEndpointNotConfiguredException(string apiCode, string displayName)
    : Exception($"API PMIS '{displayName}' ({apiCode}) chưa được cấu hình hoặc đang tắt. Vào Quản trị hệ thống > Cấu hình kết nối PMIS để thiết lập.")
{
    public string ApiCode { get; } = apiCode;
}
