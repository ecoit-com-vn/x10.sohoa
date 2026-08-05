using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Diagnostics;
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
                errorMessage = "loai chi nhan gia tri 1 (tram bien ap) hoac 2 (duong day).";
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
                errorMessage = "loai chi nhan gia tri 1 (tram bien ap) hoac 2 (duong day).";
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

    //private static string CreateQrCodeBase64(string content)
    //{
    //    using var generator = new QRCodeGenerator();
    //    using var qrCodeData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
    //    var pngBytes = new PngByteQRCode(qrCodeData).GetGraphic(12);
    //    return Convert.ToBase64String(pngBytes);
    //}
}
