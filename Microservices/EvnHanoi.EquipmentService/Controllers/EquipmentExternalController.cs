using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Security.Cryptography;
using System.Text;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/equipment")]
public class EquipmentExternalController : ControllerBase
{
    private const string PmisBaseUrl = "https://qlshx10.ecoit.com.vn/#/equipment/equipment-external?equipmentId=";
    private const string PmisBaseUrlLLTB = "https://qlshx10.ecoit.com.vn/#/equipment/equipment-factory?equipmentId=";
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExternalApiKeyValidator _externalApiKeyValidator;
    private const string keyName = "PMIS-BBXX";
    private const string keyNameLLTB = "PMIS-LLTB";

    public EquipmentExternalController(
        IEquipmentRepository equipmentRepository,
        IDocumentRepository documentRepository,
        IExternalApiKeyValidator externalApiKeyValidator)
    {
        _equipmentRepository = equipmentRepository;
        _documentRepository = documentRepository;
        _externalApiKeyValidator = externalApiKeyValidator;
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
        if (string.IsNullOrWhiteSpace(keyName) ||
            string.IsNullOrWhiteSpace(privateKey) ||
            !await _externalApiKeyValidator.IsValidAsync(keyName.Trim(), ComputeSha256(privateKey)))
        {
            return Unauthorized(new { message = "Private key không hợp lệ hoặc đã hết hạn." });
        }

        if (request.Loai is not null and not 1 and not 2)
            return BadRequest(new { message = "loai chi nhan gia tri 1 (tram bien ap) hoac 2 (duong day)." });

        if (request.Skip < 0 || request.Take is < 1 or > 1000)
            return BadRequest(new { message = "skip phải từ 0 trở lên và take phải từ 1 đến 1000." });

        if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            return BadRequest(new { message = "Từ ngày không được lớn hơn đến ngày." });

        var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

        foreach (var item in items)
        {
            item.Link = $"{PmisBaseUrl}{Uri.EscapeDataString(item.Id.ToString())}";
           // item.MaQRCode = CreateQrCodeBase64(item.Link);
        }

        return Ok(new { items, totalCount, request.Skip, request.Take });
    }
    [HttpGet("pmis-getlist-factory")]
    public async Task<IActionResult> GetPmisListEquipmentFactory(
       //   [FromHeader(Name = "X-Pmis-Key-Name")] string? keyName,
       [FromHeader(Name = "X-Pmis-Private-Key")] string? privateKey,
       [FromQuery] PmisEquipmentListRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(keyNameLLTB) ||
            string.IsNullOrWhiteSpace(privateKey) ||
            !await _externalApiKeyValidator.IsValidAsync(keyNameLLTB.Trim(), ComputeSha256(privateKey)))
        {
            return Unauthorized(new { message = "Private key không hợp lệ hoặc đã hết hạn." });
        }

        if (request.Loai is not null and not 1 and not 2)
            return BadRequest(new { message = "loai chi nhan gia tri 1 (tram bien ap) hoac 2 (duong day)." });

        if (request.Skip < 0 || request.Take is < 1 or > 1000)
            return BadRequest(new { message = "skip phải từ 0 trở lên và take phải từ 1 đến 1000." });

        if (request.TuNgay.HasValue && request.DenNgay.HasValue && request.TuNgay.Value.Date > request.DenNgay.Value.Date)
            return BadRequest(new { message = "Từ ngày không được lớn hơn đến ngày." });

        var (items, totalCount) = await _equipmentRepository.GetExternalListAsync(request);

        foreach (var item in items)
        {
            item.Link = $"{PmisBaseUrlLLTB}{Uri.EscapeDataString(item.Id.ToString())}";
            // item.MaQRCode = CreateQrCodeBase64(item.Link);
        }

        return Ok(new { items, totalCount, request.Skip, request.Take });
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
