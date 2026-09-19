using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// Danh mục đọc trực tiếp từ PMIS (không lưu DB) — dùng cho các dropdown cấu hình, hiện có màn
/// "Ánh xạ loại thiết bị PMIS" cần danh sách mã/tên loại thiết bị PMIS để admin chọn.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/sync/lookup")]
public class PmisLookupController : ControllerBase
{
    private readonly IPmisClient _pmisClient;
    private readonly IPmisEndpointConfigProvider _endpointConfigProvider;

    public PmisLookupController(IInteractivePmisClient pmisClient, IPmisEndpointConfigProvider endpointConfigProvider)
    {
        _pmisClient = pmisClient;
        _endpointConfigProvider = endpointConfigProvider;
    }

    /// <summary>
    /// Gộp loại thiết bị TBA (API 3) và loại thiết bị đường dây (API 5) — bảng ánh xạ không phân biệt
    /// nguồn nên khử trùng theo maLoaiTB, chỉ giữ nhãn nguồn để admin dễ nhận biết.
    /// </summary>
    [HttpGet("device-types")]
    public async Task<IActionResult> GetDeviceTypes()
    {
        var substationTake = (await _endpointConfigProvider.GetEndpointAsync("SUBSTATION_DEVICE_TYPE_LIST"))?.PageSize
            ?? Models.PmisPaging.DefaultPageSize;
        var lineTake = (await _endpointConfigProvider.GetEndpointAsync("LINE_DEVICE_TYPE_LIST"))?.PageSize
            ?? Models.PmisPaging.DefaultPageSize;

        try
        {
            var substation = await _pmisClient.GetSubstationDeviceTypesAsync(new PmisDeviceTypeSearchRequest { Take = substationTake });
            var line = await _pmisClient.GetLineDeviceTypesAsync(new PmisDeviceTypeSearchRequest { Take = lineTake });

            var items = substation.Items.Select(x => new { x.MaLoaiTB, x.TenLoaiTB, Source = "SUBSTATION" })
                .Concat(line.Items.Select(x => new { x.MaLoaiTB, x.TenLoaiTB, Source = "LINE" }))
                .Where(x => !string.IsNullOrWhiteSpace(x.MaLoaiTB))
                .GroupBy(x => x.MaLoaiTB)
                .Select(g => g.First())
                .OrderBy(x => x.MaLoaiTB)
                .ToList();

            return Ok(items);
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
}
