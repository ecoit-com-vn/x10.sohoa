using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models.Pmis;
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
    private const int MaxTake = 1000;

    private readonly IPmisClient _pmisClient;

    public PmisLookupController(IPmisClient pmisClient)
    {
        _pmisClient = pmisClient;
    }

    /// <summary>
    /// Gộp loại thiết bị TBA (API 3) và loại thiết bị đường dây (API 5) — bảng ánh xạ không phân biệt
    /// nguồn nên khử trùng theo maLoaiTB, chỉ giữ nhãn nguồn để admin dễ nhận biết.
    /// </summary>
    [HttpGet("device-types")]
    public async Task<IActionResult> GetDeviceTypes()
    {
        var request = new PmisDeviceTypeSearchRequest { Take = MaxTake };

        try
        {
            var substation = await _pmisClient.GetSubstationDeviceTypesAsync(request);
            var line = await _pmisClient.GetLineDeviceTypesAsync(request);

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
