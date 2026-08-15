using System.Linq;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API NỘI BỘ — SyncService gọi để lưu dữ liệu Trạm/Đường dây/Thiết bị đã đồng bộ từ PMIS.
/// - Đặt ngoài tiền tố "/api/v1/..." nên KHÔNG có route ở ApiGateway ⇒ không expose ra Internet
///   (giống InternalDossierController).
/// - [BypassDynamicPermission]: không kiểm quyền người dùng cuối (gọi service-to-service).
/// - Phòng thủ chiều sâu: bắt buộc khớp shared-secret header "X-Internal-Token".
/// </summary>
[ApiController]
[Route("internal/v1")]
[BypassDynamicPermission]
public class InternalPmisSyncController : ControllerBase
{
    private readonly IInfrastructureRepository _infrastructureRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IEquipmentPmisSpecRepository _equipmentPmisSpecRepository;
    private readonly IConfiguration _configuration;

    public InternalPmisSyncController(
        IInfrastructureRepository infrastructureRepository,
        IEquipmentRepository equipmentRepository,
        IEquipmentPmisSpecRepository equipmentPmisSpecRepository,
        IConfiguration configuration)
    {
        _infrastructureRepository = infrastructureRepository;
        _equipmentRepository = equipmentRepository;
        _equipmentPmisSpecRepository = equipmentPmisSpecRepository;
        _configuration = configuration;
    }

    [HttpGet("infrastructure/synced-pmis-codes")]
    public async Task<IActionResult> GetSyncedPmisCodes([FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;

        var rows = await _infrastructureRepository.GetSyncedPmisCodesAsync();
        return Ok(rows.Select(r => new { pmisCode = r.PmisCode, infraTypeId = r.InfraTypeId }));
    }

    [HttpPost("infrastructure/upsert-from-pmis")]
    public async Task<IActionResult> UpsertInfrastructureFromPmis(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] List<UpsertInfrastructureFromPmisRequest> items)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        if (items is null || items.Count == 0) return BadRequest(new { message = "Danh sách rỗng." });

        var results = new List<UpsertInfrastructureFromPmisResult>();
        foreach (var item in items)
        {
            try
            {
                var (id, wasCreated) = await _infrastructureRepository.UpsertFromPmisAsync(
                    item.InfraTypeId, item.PmisCode, item.Code, item.Name, item.Address, item.UnitCode, item.OperationDate);
                results.Add(new UpsertInfrastructureFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = true,
                    InfrastructureId = id,
                    WasCreated = wasCreated
                });
            }
            catch (Exception ex)
            {
                results.Add(new UpsertInfrastructureFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return Ok(results);
    }

    [HttpPost("equipment/upsert-from-pmis")]
    public async Task<IActionResult> UpsertEquipmentFromPmis(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] List<UpsertEquipmentFromPmisRequest> items)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        if (items is null || items.Count == 0) return BadRequest(new { message = "Danh sách rỗng." });

        var results = new List<UpsertEquipmentFromPmisResult>();
        foreach (var item in items)
        {
            var upsertResult = await _equipmentRepository.UpsertFromPmisAsync(
                item.PmisCode, item.Code, item.Name, item.SerialNumber,
                item.EquipmentTypeCode, item.ParentPmisCode, item.UnitCode,
                item.ManufactureYear, item.QrCodeBase64);

            if (!upsertResult.Success)
            {
                results.Add(new UpsertEquipmentFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = false,
                    ErrorMessage = upsertResult.ErrorMessage
                });
                continue;
            }

            // Thông số kỹ thuật lưu riêng — KHÔNG ghi đè EQUIPMENTS.FormValues (dữ liệu người dùng chỉnh sửa nội bộ).
            if (!string.IsNullOrWhiteSpace(item.ThongSoKyThuat))
            {
                await _equipmentPmisSpecRepository.UpsertAsync(upsertResult.EquipmentId!.Value, item.ThongSoKyThuat, null);
            }

            results.Add(new UpsertEquipmentFromPmisResult
            {
                PmisCode = item.PmisCode,
                Success = true,
                EquipmentId = upsertResult.EquipmentId,
                WasCreated = upsertResult.WasCreated
            });
        }

        return Ok(results);
    }

    private bool ValidateInternalToken(string? internalToken, out IActionResult? errorResult)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên EquipmentService." });
            return false;
        }

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
        {
            errorResult = Unauthorized(new { message = "Token nội bộ không hợp lệ." });
            return false;
        }

        errorResult = null;
        return true;
    }
}
