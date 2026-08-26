using System.Security.Claims;
using System.Text.Json;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// Đồng bộ thủ công (module 3): Bước 1 tìm kiếm (search, không lưu DB) — Bước 2 người dùng chọn
/// bằng checkbox trên FE — Bước 3 Lưu (save) đúng những bản ghi FE đã gửi lại (không fetch lại PMIS).
/// Logic upsert dùng chung với đồng bộ tự động qua <see cref="IPmisSyncExecutionService"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/sync/manual")]
public class PmisManualSyncController : ControllerBase
{
    private readonly IPmisClient _pmisClient;
    private readonly IPmisSyncExecutionService _executionService;
    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;

    public PmisManualSyncController(
        IPmisClient pmisClient,
        IPmisSyncExecutionService executionService,
        ISyncConfigRepository syncConfigRepository,
        ISyncHistoryRepository syncHistoryRepository)
    {
        _pmisClient = pmisClient;
        _executionService = executionService;
        _syncConfigRepository = syncConfigRepository;
        _syncHistoryRepository = syncHistoryRepository;
    }

    [HttpPost("{objectType}/search")]
    public async Task<IActionResult> Search(string objectType, [FromBody] PmisManualSearchRequest request)
    {
        var normalizedType = objectType.ToUpperInvariant();
        if (!SyncObjectType.IsValid(normalizedType))
            return BadRequest(new { message = "Đối tượng đồng bộ không hợp lệ." });

        try
        {
            var response = normalizedType switch
            {
                SyncObjectType.Substation => await SearchSubstationsAsync(request),
                SyncObjectType.TransmissionLine => await SearchLinesAsync(request),
                SyncObjectType.Equipment => await SearchEquipmentsAsync(request),
                _ => throw new InvalidOperationException()
            };
            return Ok(response);
        }
        catch (PmisEndpointNotConfiguredException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (Exception ex) when (PmisUpstreamFailure.Matches(ex))
        {
            return StatusCode(503, new { message = PmisUpstreamFailure.UserMessage(ex) });
        }
    }

    [HttpPost("{objectType}/save")]
    public async Task<IActionResult> Save(string objectType, [FromBody] PmisManualSaveRequest request)
    {
        var normalizedType = objectType.ToUpperInvariant();
        if (!SyncObjectType.IsValid(normalizedType))
            return BadRequest(new { message = "Đối tượng đồng bộ không hợp lệ." });
        if (request.Items.Count == 0)
            return BadRequest(new { message = "Chưa chọn bản ghi nào để đồng bộ." });

        var syncConfig = await _syncConfigRepository.GetByObjectTypeAsync(normalizedType);
        var historyId = await _syncHistoryRepository.CreateAsync(new SyncHistory
        {
            SyncConfigId = syncConfig?.Id ?? string.Empty,
            ObjectType = normalizedType,
            SyncType = SyncType.Manual,
            StartTime = DateTime.UtcNow,
            Status = SyncHistoryStatus.Running,
            CreatedBy = CurrentUserName()
        });

        var (successCount, failedCount, errors) = normalizedType switch
        {
            SyncObjectType.Substation => await _executionService.SyncInfrastructureAsync(1, historyId, request.Items),
            SyncObjectType.TransmissionLine => await _executionService.SyncInfrastructureAsync(2, historyId, request.Items),
            SyncObjectType.Equipment => await _executionService.SyncEquipmentAsync(historyId, request.Items),
            _ => throw new InvalidOperationException()
        };

        var finalStatus = successCount == 0 ? SyncHistoryStatus.Failed : SyncHistoryStatus.Success;
        await _syncHistoryRepository.CompleteAsync(
            historyId, finalStatus, request.Items.Count, successCount, failedCount,
            errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null);

        return Ok(new PmisManualSaveResponse
        {
            SyncHistoryId = historyId,
            Total = request.Items.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            Errors = errors
        });
    }

    private async Task<PmisManualSearchResponse> SearchSubstationsAsync(PmisManualSearchRequest r)
    {
        var result = await _pmisClient.GetSubstationsAsync(new PmisSubstationSearchRequest
        {
            MaDonVi = r.MaDonVi,
            LoaiTBA = r.LoaiTBA,
            TuNgay = r.TuNgay,
            DenNgay = r.DenNgay,
            Skip = r.Skip,
            Take = r.Take
        });

        return new PmisManualSearchResponse
        {
            Total = result.Total,
            Items = result.Items.Select(item => new PmisSyncPreviewItemDto
            {
                PmisCode = item.MaTBA,
                DisplayName = item.TenTBA,
                RawData = JsonSerializer.SerializeToElement(item)
            }).ToList()
        };
    }

    private async Task<PmisManualSearchResponse> SearchLinesAsync(PmisManualSearchRequest r)
    {
        var result = await _pmisClient.GetLinesAsync(new PmisLineSearchRequest
        {
            MaDonVi = r.MaDonVi,
            MaLoaiDuongDay = r.MaLoaiDuongDay,
            TuNgay = r.TuNgay,
            DenNgay = r.DenNgay,
            Skip = r.Skip,
            Take = r.Take
        });

        return new PmisManualSearchResponse
        {
            Total = result.Total,
            Items = result.Items.Select(item => new PmisSyncPreviewItemDto
            {
                PmisCode = item.MaDuongDay,
                DisplayName = item.TenDuongDay,
                RawData = JsonSerializer.SerializeToElement(item)
            }).ToList()
        };
    }

    private async Task<PmisManualSearchResponse> SearchEquipmentsAsync(PmisManualSearchRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.MaTBA) && string.IsNullOrWhiteSpace(r.MaDuongDay))
            throw new InvalidOperationException("Cần chọn Trạm biến áp (maTBA) hoặc Đường dây (maDuongDay) để tìm thiết bị.");

        if (!string.IsNullOrWhiteSpace(r.MaTBA))
        {
            var result = await _pmisClient.GetSubstationDevicesAsync(new PmisSubstationDeviceSearchRequest
            {
                MaTBA = r.MaTBA,
                MaLoaiTB = r.MaLoaiTB,
                MaDonVi = r.MaDonVi,
                NamSanXuat = r.NamSanXuat,
                TinhTrang = r.TinhTrang,
                TuNgay = r.TuNgay,
                DenNgay = r.DenNgay,
                Skip = r.Skip,
                Take = r.Take
            });
            return new PmisManualSearchResponse
            {
                Total = result.Total,
                Items = result.Items.Select(item => new PmisSyncPreviewItemDto
                {
                    PmisCode = item.MaThietBi,
                    DisplayName = item.TenThietBi,
                    RawData = JsonSerializer.SerializeToElement(item)
                }).ToList()
            };
        }

        var lineResult = await _pmisClient.GetLineDevicesAsync(new PmisLineDeviceSearchRequest
        {
            MaDuongDay = r.MaDuongDay,
            MaLoaiTB = r.MaLoaiTB,
            MaDonVi = r.MaDonVi,
            KemQRCode = r.KemQRCode,
            TuNgay = r.TuNgay,
            DenNgay = r.DenNgay,
            Skip = r.Skip,
            Take = r.Take
        });
        return new PmisManualSearchResponse
        {
            Total = lineResult.Total,
            Items = lineResult.Items.Select(item => new PmisSyncPreviewItemDto
            {
                PmisCode = item.MaTB,
                DisplayName = item.TenTB,
                RawData = JsonSerializer.SerializeToElement(item)
            }).ToList()
        };
    }

    private string? CurrentUserName() =>
        User.FindFirstValue("full_name") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
