using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

/// <summary>
/// API NỘI BỘ tra cứu tài khoản theo đơn vị — dùng bởi NotificationService để xác định người nhận
/// khi phát sinh thông báo theo đơn vị (chuyển thiết bị sang TBA mới, chuyển hồ sơ thiết bị).
/// - Đặt ngoài tiền tố "/api/v1/..." nên KHÔNG có route ở ApiGateway ⇒ không expose ra Internet.
/// - [BypassDynamicPermission]: không kiểm quyền người dùng cuối (gọi service-to-service).
/// - Phòng thủ chiều sâu: bắt buộc khớp shared-secret header "X-Internal-Token".
/// Không gắn [Authorize] vì caller là service token, không phải JWT người dùng — khác với
/// GET api/v1/users vốn lọc theo unit_id/role của NGƯỜI GỌI, không phù hợp để tra cứu đơn vị bất kỳ.
/// </summary>
[ApiController]
[Route("internal/v1/users")]
[BypassDynamicPermission]
public class InternalUsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public InternalUsersController(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    [HttpGet("by-unit/{unitId:long}")]
    public async Task<IActionResult> GetActiveUserIdsByUnit(
        long unitId,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
            return StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên IdentityService." });

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Token nội bộ không hợp lệ." });

        var (items, _) = await _userRepository.GetPagedAsync(
            page: 1,
            pageSize: 5000,
            keyword: null,
            organizationUnitId: unitId,
            isActive: true,
            includeDescendants: false);

        var userIds = items.Select(u => u.Id).ToList();
        return Ok(new { userIds });
    }
}
