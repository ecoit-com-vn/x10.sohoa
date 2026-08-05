using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("internal/v1/system-params")]
[AllowAnonymous] // Cho phép endpoint dùng X-Internal-Token thay cho JWT
[BypassDynamicPermission]
public sealed class InternalSystemParamsController : ControllerBase
{
    private readonly ISystemParamRepository _systemParamRepository;
    private readonly IConfiguration _configuration;

    public InternalSystemParamsController(
        ISystemParamRepository systemParamRepository,
        IConfiguration configuration)
    {
        _systemParamRepository = systemParamRepository;
        _configuration = configuration;
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetByKey(
        string key,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
            return StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên IdentityService." });

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Token nội bộ không hợp lệ." });

        var systemParam = await _systemParamRepository.GetByKeyAsync(key);
        if (systemParam is null)
            return NotFound(new { message = "Không tìm thấy tham số hệ thống." });

        return Ok(new
        {
            systemParam.ParamKey,
            systemParam.ParamValue,
            systemParam.DataType
        });
    }
}
