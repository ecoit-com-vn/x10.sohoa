using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/equipment")]
public class EquipmentExternalController : ControllerBase
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExternalApiKeyValidator _externalApiKeyValidator;
    private readonly IExternalApiCallLogRepository _externalApiCallLogRepository;
    private readonly ISystemParamRepository _systemParamRepository;
    private readonly ILogger<EquipmentExternalController> _logger;
    private const string keyName = "PMIS-BBXX";
    private const string keyNameLLTB = "PMIS-LLTB";
    private const string keyNameCBM = "PMIS-CBM";
    private const string keyNameTSVH = "PMIS-TSVH";
    private const string keyNameTSKT = "PMIS-TSKT";
    public EquipmentExternalController(
        IEquipmentRepository equipmentRepository,
        IDocumentRepository documentRepository,
        IExternalApiKeyValidator externalApiKeyValidator,
        IExternalApiCallLogRepository externalApiCallLogRepository,
        ISystemParamRepository systemParamRepository,
        ILogger<EquipmentExternalController> logger)
    {
        _equipmentRepository = equipmentRepository;
        _documentRepository = documentRepository;
        _externalApiKeyValidator = externalApiKeyValidator;
        _externalApiCallLogRepository = externalApiCallLogRepository;
        _systemParamRepository = systemParamRepository;
        _logger = logger;
    }

    [HttpGet("external/factory-acceptance")]
    public async Task<IActionResult> GetFactoryAcceptanceEquipmentList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 1000,
        [FromQuery] string? keyword = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        var filter = new DossierDocumentFilterDto
        {
            Page = page,
            PageSize = pageSize,
            Keyword = keyword
        };
        var (items, totalCount) = await _documentRepository.GetPublishedFactoryAcceptanceDocumentsAsync(filter);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("pmis-getlist")]
    public async Task<IActionResult> GetPmisListEquipment(
     //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
        [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
        [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyName) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyName.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }

            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);
            pmisUrl = pmisUrl + "/#/equipment/equipment-external?equipmentId=";
            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
               // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist", keyName, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-factory")]
    public async Task<IActionResult> GetPmisListEquipmentFactory(
       //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
       [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
       [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameLLTB) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameLLTB.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }
            pmisUrl = pmisUrl + "/#/equipment/equipment-factory?equipmentId=";
            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
                // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-factory", keyNameLLTB, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-cbm")]
    public async Task<IActionResult> GetPmisListEquipmentCBM(
     //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
     [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
     [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameCBM) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameCBM.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }
            pmisUrl = pmisUrl + "/#/equipment/equipment-cbm?equipmentId=";
            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
                // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-cbm", keyNameCBM, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-tsvh")]
    public async Task<IActionResult> GetPmisListEquipmentTSVH(
   //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
   [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
   [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameTSVH) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameTSVH.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            if (request.Loai is not null and not 1 and not 2)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
                return BadRequest(new { message = errorMessage });
            }

            if (request.Skip < 0 || request.Take is < 1 or > 1000)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
                return BadRequest(new { message = errorMessage });
            }

            if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            {
                statusCode = StatusCodes.Status400BadRequest;
                errorMessage = "Từ ngày không được lớn hơn đến ngày.";
                return BadRequest(new { message = errorMessage });
            }

            var pmisUrl = await _systemParamRepository.GetValueAsync("PmisUrl");
            if (string.IsNullOrWhiteSpace(pmisUrl))
            {
                statusCode = StatusCodes.Status500InternalServerError;
                errorMessage = "Chưa cấu hình tham số hệ thống PmisUrl.";
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage });
            }
            pmisUrl = pmisUrl + "/#/equipment/equipment-detail?equipmentId=";
            var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

            foreach (var item in items)
            {
                item.Link = $"{pmisUrl}{Uri.EscapeDataString(item.Id.ToString())}";
                // item.MaQRCode = CreateQrCodeBase64(item.Link);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { items, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-tsvh", keyNameTSVH, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }
    [HttpGet("pmis-getlist-tskt")]
    public async Task<IActionResult> GetPmisListEquipmentTSKT(
       [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
       [FromQuery] PmisEquipmentListRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        long? apiKeyId = null;
        int statusCode = StatusCodes.Status200OK;
        string? errorMessage = null;
        string? responseSummary = null;

        try
        {
            if (string.IsNullOrWhiteSpace(keyNameTSKT) || string.IsNullOrWhiteSpace(privateKey))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            apiKeyId = await _externalApiKeyValidator.ValidateAsync(keyNameTSKT.Trim(), ComputeSha256(privateKey));
            if (apiKeyId is null)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                errorMessage = "Private key không hợp lệ hoặc đã hết hạn.";
                return Unauthorized(new { message = errorMessage });
            }

            //if (request.Loai is not null and not 1 and not 2)
            //{
            //    statusCode = StatusCodes.Status400BadRequest;
            //    errorMessage = "Loại chỉ nhận giá trị 1 (trạm biến áp) hoặc 2 (đường dây).";
            //    return BadRequest(new { message = errorMessage });
            //}

            //if (request.Skip < 0 || request.Take is < 1 or > 1000)
            //{
            //    statusCode = StatusCodes.Status400BadRequest;
            //    errorMessage = "skip phải từ 0 trở lên và take phải từ 1 đến 1000.";
            //    return BadRequest(new { message = errorMessage });
            //}

            //if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            //{
            //    statusCode = StatusCodes.Status400BadRequest;
            //    errorMessage = "Từ ngày không được lớn hơn đến ngày.";
            //    return BadRequest(new { message = errorMessage });
            //}

            var (data, totalCount) = await _equipmentRepository.GetExternalListWithItemsAsync(request);

            foreach (var item in data)
            {
                item.Items = BuildTechnicalParameters(item.FormSchema, item.FormValues);
            }

            responseSummary = $"totalCount={totalCount}";
            return Ok(new { data, totalCount, request.Skip, request.Take });
        }
        catch (Exception ex)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            await LogApiCallAsync("pmis-getlist-tskt", keyNameTSKT, apiKeyId, statusCode, errorMessage, responseSummary, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task LogApiCallAsync(
        string endpoint,
        string requestedKeyName,
        long? apiKeyId,
        int statusCode,
        string? errorMessage,
        string? responseSummary,
        long durationMs)
    {
        try
        {
            await _externalApiCallLogRepository.LogAsync(new ExternalApiCallLogEntry
            {
                ApiKeyId = apiKeyId,
                KeyName = requestedKeyName,
                Endpoint = $"api/v1/equipment/{endpoint}",
                HttpMethod = Request.Method,
                RequestQuery = Request.QueryString.HasValue ? Request.QueryString.Value : null,
                RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                StatusCode = statusCode,
                IsSuccess = statusCode is >= 200 and < 300,
                DurationMs = durationMs,
                ResponseSummary = responseSummary,
                ErrorMessage = errorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write external API call log for endpoint {Endpoint}.", endpoint);
        }
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>
    /// Bảng ánh xạ TĨNH label thông số kỹ thuật (theo yêu cầu quản lý thông tin thiết bị lưới điện cao áp)
    /// → tên property cố định trên <see cref="TechnicalParametersDto"/>. Đây là danh sách khai báo tay,
    /// KHÔNG suy ra bằng thuật toán, nên key trả về luôn cố định vĩnh viễn dù label bị chỉnh sửa trong Form
    /// Builder sau này. Thêm label mới vào đây + thêm property tương ứng trên TechnicalParametersDto khi cần
    /// trả thêm 1 thông số kỹ thuật cố định.
    /// </summary>
    private static readonly Dictionary<string, string> TechnicalParameterLabelToPropertyName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hãng SX"] = nameof(TechnicalParametersDto.HangSx),
        ["Nước SX"] = nameof(TechnicalParametersDto.NuocSx),
        ["Kiểu"] = nameof(TechnicalParametersDto.Kieu),
        ["Kiểu (Type)"] = nameof(TechnicalParametersDto.KieuType),
        ["Kiểu cách điện"] = nameof(TechnicalParametersDto.KieuCachDien),
        ["Loại máy"] = nameof(TechnicalParametersDto.LoaiMay),
        ["Nấc phân áp"] = nameof(TechnicalParametersDto.NacPhanAp),
        ["Tần số (Hz)"] = nameof(TechnicalParametersDto.TanSoHz),
        ["Tần số"] = nameof(TechnicalParametersDto.TanSo),
        ["Công suất (kVA)"] = nameof(TechnicalParametersDto.CongSuatKva),
        ["Công suất"] = nameof(TechnicalParametersDto.CongSuat),
        ["Công suất (W)"] = nameof(TechnicalParametersDto.CongSuatW),
        ["Công suất cắt"] = nameof(TechnicalParametersDto.CongSuatCat),
        ["Tổn hao không tải (KW)"] = nameof(TechnicalParametersDto.TonHaoKhongTaiKw),
        ["Loại dầu"] = nameof(TechnicalParametersDto.LoaiDau),
        ["Loại sứ cách điện"] = nameof(TechnicalParametersDto.LoaiSuCachDien),
        ["Trọng lượng dầu (kg)"] = nameof(TechnicalParametersDto.TrongLuongDauKg),
        ["Kiểu làm mát"] = nameof(TechnicalParametersDto.KieuLamMat),
        ["Tiêu chuẩn áp dụng"] = nameof(TechnicalParametersDto.TieuChuanApDung),
        ["Độ tăng nhiệt độ cực đại lớp dầu trên cùng"] = nameof(TechnicalParametersDto.DoTangNhietDoCucDaiLopDauTrenCung),
        ["Độ tăng nhiệt độ cực đại cuộn dây"] = nameof(TechnicalParametersDto.DoTangNhietDoCucDaiCuonDay),
        ["Khả năng quá tải"] = nameof(TechnicalParametersDto.KhaNangQuaTai),
        ["Kích thước D, R, C (m)"] = nameof(TechnicalParametersDto.KichThuocDRCM),
        ["Điện áp định mức"] = nameof(TechnicalParametersDto.DienApDinhMuc),
        ["Điện áp định mức (kV)"] = nameof(TechnicalParametersDto.DienApDinhMucKv),
        ["Điện áp danh định (KV)"] = nameof(TechnicalParametersDto.DienApDanhDinhKv),
        ["Điện áp (V)"] = nameof(TechnicalParametersDto.DienApV),
        ["Chủng loại"] = nameof(TechnicalParametersDto.ChungLoai),
        ["Dòng điện xung (3s)"] = nameof(TechnicalParametersDto.DongDienXung3s),
        ["Số pha"] = nameof(TechnicalParametersDto.SoPha),
        ["Số lưỡi tiếp địa"] = nameof(TechnicalParametersDto.SoLuoiTiepDia),
        ["Phân loại"] = nameof(TechnicalParametersDto.PhanLoai),
        ["Loại dao"] = nameof(TechnicalParametersDto.LoaiDao),
        ["Dòng điện định mức (A)"] = nameof(TechnicalParametersDto.DongDienDinhMucA),
        ["Dòng điện cắt định mức (A)"] = nameof(TechnicalParametersDto.DongDienCatDinhMucA),
        ["Dòng điện ngắn mạch định mức (A)"] = nameof(TechnicalParametersDto.DongDienNganMachDinhMucA),
        ["Môi trường cách điện"] = nameof(TechnicalParametersDto.MoiTruongCachDien),
        ["Điện áp chịu đựng ở tần số công nghiệp (kV)"] = nameof(TechnicalParametersDto.DienApChiuDungOTanSoCongNghiepKv),
        ["Dòng định mức cuộn bảo vệ"] = nameof(TechnicalParametersDto.DongDinhMucCuonBaoVe),
        ["Dòng định mức cuộn đo lường"] = nameof(TechnicalParametersDto.DongDinhMucCuonDoLuong),
        ["Dòng điện phía sơ cấp"] = nameof(TechnicalParametersDto.DongDienPhiaSoCap),
        ["Tổ đấu dây"] = nameof(TechnicalParametersDto.ToDauDay),
        ["Loại chống sét"] = nameof(TechnicalParametersDto.LoaiChongSet),
        ["Cấp chống sét"] = nameof(TechnicalParametersDto.CapChongSet),
        ["Điện áp làm việc liên tục"] = nameof(TechnicalParametersDto.DienApLamViecLienTuc),
        ["Hạt nổ chống sét"] = nameof(TechnicalParametersDto.HatNoChongSet),
        ["Vật liệu vỏ ngoài"] = nameof(TechnicalParametersDto.VatLieuVoNgoai),
        ["Kiểu Tụ"] = nameof(TechnicalParametersDto.KieuTu),
        ["Dòng điện làm việc max"] = nameof(TechnicalParametersDto.DongDienLamViecMax),
        ["Điện dung tụ"] = nameof(TechnicalParametersDto.DienDungTu),
        ["Kiểu GIS (Type)"] = nameof(TechnicalParametersDto.KieuGisType),
        ["Udm (kV) (Rate voltage)"] = nameof(TechnicalParametersDto.UdmKvRateVoltage),
        ["Idm - Ngăn (A)"] = nameof(TechnicalParametersDto.IdmNganA),
        ["Idm - Thanh cái (A)"] = nameof(TechnicalParametersDto.IdmThanhCaiA),
        ["Idm - Thanh liên lạc (A)"] = nameof(TechnicalParametersDto.IdmThanhLienLacA),
        ["Inm định mức (kA)"] = nameof(TechnicalParametersDto.InmDinhMucKa),
        ["Thời gian ngắn mạch định mức (s)"] = nameof(TechnicalParametersDto.ThoiGianNganMachDinhMucS),
        ["Dòng điện đỉnh định mức (kA)"] = nameof(TechnicalParametersDto.DongDienDinhDinhMucKa),
        ["Áp suất khí cao (bar)"] = nameof(TechnicalParametersDto.ApSuatKhiCaoBar),
        ["Serial"] = nameof(TechnicalParametersDto.Serial),
        ["Năm sản xuất"] = nameof(TechnicalParametersDto.NamSanXuat),
        ["Dòng tải cực đại (A)"] = nameof(TechnicalParametersDto.DongTaiCucDaiA),
        ["Dòng khởi động (A)"] = nameof(TechnicalParametersDto.DongKhoiDongA),
        ["Tụt khí SF6"] = nameof(TechnicalParametersDto.TutKhiSf6),
        ["Tải định mức (VA)"] = nameof(TechnicalParametersDto.TaiDinhMucVa),
        ["Tải định mức"] = nameof(TechnicalParametersDto.TaiDinhMuc),
        ["Cấp cách điện"] = nameof(TechnicalParametersDto.CapCachDien),
        ["Định mức/Chịu đựng NM tần số công nghiệp"] = nameof(TechnicalParametersDto.DinhMucChiuDungNmTanSoCongNghiep),
        ["Định mức chịu đựng xung sét (kV)"] = nameof(TechnicalParametersDto.DinhMucChiuDungXungSetKv),
        ["Cấp chính xác các cuộn dây"] = nameof(TechnicalParametersDto.CapChinhXacCacCuonDay),
        ["Dải đo (%)"] = nameof(TechnicalParametersDto.DaiDo),
        ["Dải đo (%) (Measuring range)"] = nameof(TechnicalParametersDto.DaiDoMeasuringRange),
        ["Cấp chính xác (1)"] = nameof(TechnicalParametersDto.CapChinhXac1),
        ["Cấp chính xác (2)"] = nameof(TechnicalParametersDto.CapChinhXac2),
        ["Cấp chính xác (3)"] = nameof(TechnicalParametersDto.CapChinhXac3),
        ["Cấp chính xác (4)"] = nameof(TechnicalParametersDto.CapChinhXac4),
        ["Cấp chính xác (5)"] = nameof(TechnicalParametersDto.CapChinhXac5),
        ["Tỉ số biến (1)"] = nameof(TechnicalParametersDto.TiSoBien1),
        ["Tỉ số biến (2)"] = nameof(TechnicalParametersDto.TiSoBien2),
        ["Tỉ số biến (3)"] = nameof(TechnicalParametersDto.TiSoBien3),
        ["Tỉ số biến (4)"] = nameof(TechnicalParametersDto.TiSoBien4),
        ["Tỉ số biến (5)"] = nameof(TechnicalParametersDto.TiSoBien5),
        ["Công suất định mức (1) (VA)"] = nameof(TechnicalParametersDto.CongSuatDinhMuc1Va),
        ["Công suất định mức (2) (VA)"] = nameof(TechnicalParametersDto.CongSuatDinhMuc2Va),
        ["Công suất định mức (3) (VA)"] = nameof(TechnicalParametersDto.CongSuatDinhMuc3Va),
        ["Công suất định mức (3)"] = nameof(TechnicalParametersDto.CongSuatDinhMuc3Va),
        ["Công suất định mức (4) (VA)"] = nameof(TechnicalParametersDto.CongSuatDinhMuc4Va),
        ["Công suất định mức (5) (VA)"] = nameof(TechnicalParametersDto.CongSuatDinhMuc5Va),
        ["Cấp cách điện định mức"] = nameof(TechnicalParametersDto.CapCachDienDinhMuc),
        ["Tỉ số biến"] = nameof(TechnicalParametersDto.TiSoBien),
        ["Kiểu HGIS"] = nameof(TechnicalParametersDto.KieuHgis),
        ["Kiểu truyền động"] = nameof(TechnicalParametersDto.KieuTruyenDong),
        ["Kiểu truyền động lưỡi dao"] = nameof(TechnicalParametersDto.KieuTruyenDongLuoiDao),
        ["Dòng điện ổn định nhiệt khi ngắn mạch (kA)"] = nameof(TechnicalParametersDto.DongDienOnDinhNhietKhiNganMachKa),
        ["Dòng điện ổn định động khi ngắn mạch (kA)"] = nameof(TechnicalParametersDto.DongDienOnDinhDongKhiNganMachKa),
    };

    /// <summary>
    /// Bảng ánh xạ Label (đã chuẩn hoá) → PropertyInfo tương ứng trên <see cref="TechnicalParametersDto"/>,
    /// build 1 lần khi service khởi động từ <see cref="TechnicalParameterLabelToPropertyName"/>.
    /// </summary>
    private static readonly Dictionary<string, PropertyInfo> TechnicalParameterPropertiesByLabel = BuildTechnicalParameterPropertyMap();

    private static Dictionary<string, PropertyInfo> BuildTechnicalParameterPropertyMap()
    {
        var type = typeof(TechnicalParametersDto);
        var map = new Dictionary<string, PropertyInfo>();

        foreach (var (label, propertyName) in TechnicalParameterLabelToPropertyName)
        {
            var property = type.GetProperty(propertyName)
                ?? throw new InvalidOperationException($"TechnicalParametersDto không có property '{propertyName}'.");
            map[NormalizeLabelForLookup(label)] = property;
        }

        return map;
    }

    /// <summary>Chuẩn hoá label để so khớp bảng tĩnh: chữ thường, gộp khoảng trắng thừa, bỏ khoảng trắng đầu/cuối.</summary>
    private static string NormalizeLabelForLookup(string label) =>
        System.Text.RegularExpressions.Regex.Replace(label.Trim().ToLowerInvariant(), @"\s+", " ");

    /// <summary>
    /// Gán thông số kỹ thuật EAV vào các trường cố định của <see cref="TechnicalParametersDto"/>: đọc danh sách
    /// field từ FormSchema (biểu mẫu theo loại thiết bị), tra giá trị trong FormValues, rồi gán vào đúng property
    /// theo label (qua <see cref="TechnicalParameterPropertiesByLabel"/>). Label chưa được khai báo trong
    /// <see cref="KnownTechnicalParameterLabels"/> sẽ bị bỏ qua vì không có field cố định nào để gán.
    /// </summary>
    private static TechnicalParametersDto BuildTechnicalParameters(string? formSchemaJson, string? formValuesJson)
    {
        var result = new TechnicalParametersDto();
        if (string.IsNullOrWhiteSpace(formSchemaJson))
            return result;

        System.Text.Json.JsonElement? formValues = null;
        if (!string.IsNullOrWhiteSpace(formValuesJson))
        {
            try
            {
                formValues = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(formValuesJson);
            }
            catch (System.Text.Json.JsonException)
            {
                formValues = null;
            }
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(formSchemaJson);
            var root = doc.RootElement;

            IEnumerable<System.Text.Json.JsonElement> fieldElements = root.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Array => root.EnumerateArray(),
                System.Text.Json.JsonValueKind.Object when TryGetPropertyIgnoreCase(root, "fields", out var fieldsEl)
                                         && fieldsEl.ValueKind == System.Text.Json.JsonValueKind.Array
                    => fieldsEl.EnumerateArray(),
                _ => Array.Empty<System.Text.Json.JsonElement>()
            };

            foreach (var field in fieldElements)
            {
                // Khóa tra giá trị trong FormValues vẫn phải dùng đúng key gốc mà form đã lưu (name/key/id).
                var lookupKey = ResolveSchemaFieldName(field);
                if (string.IsNullOrWhiteSpace(lookupKey))
                    continue;

                if (!TechnicalParameterPropertiesByLabel.TryGetValue(
                        NormalizeLabelForLookup((ReadSchemaString(field, "label", "Label") ?? lookupKey).Trim()),
                        out var property))
                {
                    continue; // label chưa khai báo trong bảng tĩnh — không có field cố định để gán
                }

                var value = formValues.HasValue && TryGetPropertyIgnoreCase(formValues.Value, lookupKey, out var valueEl)
                    ? ReadJsonScalarAsString(valueEl)
                    : null;

                if (value != null)
                {
                    property.SetValue(result, value);
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Biểu mẫu không hợp lệ — trả về DTO rỗng (toàn bộ property null), không chặn response chung.
        }

        return result;
    }

    /// <summary>Lấy mã trường EAV — ưu tiên name/key có giá trị, fallback id (form builder hay để name rỗng).</summary>
    private static string? ResolveSchemaFieldName(System.Text.Json.JsonElement item)
    {
        foreach (var property in new[] { "name", "Name", "key", "Key", "id", "Id", "fieldName", "FieldName" })
        {
            if (!TryGetPropertyIgnoreCase(item, property, out var el))
                continue;

            var value = ReadJsonScalarAsString(el);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ReadSchemaString(System.Text.Json.JsonElement item, params string[] propertyNames)
    {
        foreach (var property in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(item, property, out var el))
                continue;

            var value = ReadJsonScalarAsString(el);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(System.Text.Json.JsonElement element, string propertyName, out System.Text.Json.JsonElement value)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
            return true;

        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? ReadJsonScalarAsString(System.Text.Json.JsonElement el) =>
        el.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => el.GetString(),
            System.Text.Json.JsonValueKind.Number => el.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => null
        };

    //private static string CreateQrCodeBase64(string content)
    //{
    //    using var generator = new QRCodeGenerator();
    //    using var qrCodeData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
    //    var pngBytes = new PngByteQRCode(qrCodeData).GetGraphic(12);
    //    return Convert.ToBase64String(pngBytes);
    //}
}
