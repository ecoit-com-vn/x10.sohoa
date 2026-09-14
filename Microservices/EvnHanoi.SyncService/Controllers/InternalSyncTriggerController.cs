using EvnHanoi.Infrastructure.Security;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// API NỘI BỘ — EquipmentService gọi để yêu cầu chạy sớm 1 lượt đồng bộ đầy đủ (Trạm/Đường dây/Thiết bị),
/// thay vì chờ đúng lịch cấu hình. Không tự chạy đồng bộ ngay tại đây — chỉ đặt NEXT_SYNC_AT = ngay bây
/// giờ, PmisScheduledSyncJob (Quartz, tick mỗi phút, đã chạy sẵn) sẽ tự nhận thấy "đã tới hạn" và chạy 1
/// lượt "chọn tất cả" trong vòng &lt;=1 phút sau. Cùng khuôn với InternalPmisSyncController
/// (EquipmentService): đặt ngoài "/api/v1/..." nên không có route ở ApiGateway, [BypassDynamicPermission],
/// bắt buộc khớp shared-secret header "X-Internal-Token".
/// </summary>
[ApiController]
[Route("internal/v1")]
[BypassDynamicPermission]
public class InternalSyncTriggerController : ControllerBase
{
    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly IConfiguration _configuration;

    public InternalSyncTriggerController(ISyncConfigRepository syncConfigRepository, IConfiguration configuration)
    {
        _syncConfigRepository = syncConfigRepository;
        _configuration = configuration;
    }

    /// <summary>Đánh dấu Trạm/Đường dây/Thiết bị "đã tới hạn" đồng bộ ngay — dùng khi vừa thêm 1 ánh xạ
    /// đơn vị PMIS mới (PMIS_UNIT_CODE_MAPPING), để lượt đồng bộ đầy đủ kế tiếp tự tính lại UnitId cho
    /// những Trạm/Đường dây/Thiết bị trước đó không xác định được đơn vị. Đối tượng đang tắt tự động vẫn
    /// giữ nguyên (không tự bật lại thay người dùng).</summary>
    [HttpPost("sync-config/trigger-now")]
    public async Task<IActionResult> TriggerNow([FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;

        await _syncConfigRepository.MarkDueNowAsync(
            [SyncObjectType.Substation, SyncObjectType.TransmissionLine, SyncObjectType.Equipment]);

        return Ok();
    }

    private bool ValidateInternalToken(string? internalToken, out IActionResult? errorResult)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên SyncService." });
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
