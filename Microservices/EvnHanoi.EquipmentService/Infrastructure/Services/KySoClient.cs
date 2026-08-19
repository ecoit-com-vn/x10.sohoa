using System.Net.Http.Json;
using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs.DigitalSignature;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Services;

/// <summary>
/// Triển khai <see cref="IKySoClient"/> — gọi 3 API ký số ngoài qua HttpClient đặt tên "KySo".
/// KHÔNG BAO GIỜ log toàn bộ request/response (chứa base64 PDF, ảnh chữ ký, mật khẩu P12) — chỉ
/// log kết quả tổng quát (thành công/thất bại, ns_id, serial đã che bớt) ở Information, và chi tiết
/// exception (không kèm payload) ở Error.
/// </summary>
public class KySoClient : IKySoClient
{
    private const string SerialNumberPath = "api/DigitalSignature/lay-thong-tin-serial-number";
    private const string SignatureImagePath = "hrms/api/Hrms/lay-anh-chu-ky";
    private const string SignPdfPath = "kyso/api/DigitalSignature/sign-pdf-base64-image";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KySoClient> _logger;

    public KySoClient(IHttpClientFactory httpClientFactory, ILogger<KySoClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KySoSerialNumberData?> GetSerialNumberAsync(long nsId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("KySo");
            var request = new KySoSerialNumberRequest { NsId = nsId };
            using var response = await client.PostAsJsonAsync(SerialNumberPath, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<KySoSerialNumberResponse>(JsonOptions, cancellationToken);
            var data = result?.Data?.FirstOrDefault();

            _logger.LogInformation(
                "KySo GetSerialNumber ns_id={NsId} status={Status} found={Found}",
                nsId, result?.Status, data != null);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KySo GetSerialNumber thất bại cho ns_id={NsId}", nsId);
            throw;
        }
    }

    public async Task<string?> GetSignatureImageAsync(long nsId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("KySo");
            var request = new KySoSignatureImageRequest { NsId = nsId, Type = 1 };
            using var response = await client.PostAsJsonAsync(SignatureImagePath, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<KySoSignatureImageResponse>(JsonOptions, cancellationToken);

            _logger.LogInformation(
                "KySo GetSignatureImage ns_id={NsId} status={Status} hasImage={HasImage}",
                nsId, result?.Status, !string.IsNullOrEmpty(result?.Data));

            return result?.Status == true ? result.Data : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KySo GetSignatureImage thất bại cho ns_id={NsId}", nsId);
            throw;
        }
    }

    public async Task<KySoSignPdfResultData> SignPdfAsync(KySoSignPdfRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("KySo");
            using var response = await client.PostAsJsonAsync(SignPdfPath, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<KySoSignPdfResponse>(JsonOptions, cancellationToken);
            var data = result?.Data ?? new KySoSignPdfResultData { Status = false, ObjectError = "Không nhận được phản hồi từ API ký số." };

            // Chỉ log kết quả cấp cao — KHÔNG log request.FileBase64/FileImageBase64/Password hay
            // data.SignedFileBase64.
            _logger.LogInformation(
                "KySo SignPdf serialNumber={SerialNumberMasked} outerStatus={OuterStatus} signStatus={SignStatus}",
                MaskSerial(request.SerialNumber), result?.Status, data.Status);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KySo SignPdf thất bại cho serialNumber={SerialNumberMasked}", MaskSerial(request.SerialNumber));
            throw;
        }
    }

    private static string MaskSerial(string? serial)
    {
        if (string.IsNullOrEmpty(serial) || serial.Length <= 6)
            return "***";

        return $"{serial[..3]}...{serial[^3..]}";
    }
}
