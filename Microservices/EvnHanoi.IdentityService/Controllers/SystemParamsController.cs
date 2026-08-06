using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/system-params")]
public class SystemParamsController : ControllerBase
{
    private const string AuditLogDomainKey = "AuditLogDomain";
    private readonly ISystemParamRepository _systemParamRepository;
    private readonly IHostEnvironment _hostEnvironment;

    public SystemParamsController(
        ISystemParamRepository systemParamRepository,
        IHostEnvironment hostEnvironment)
    {
        _systemParamRepository = systemParamRepository;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _systemParamRepository.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetByKey(string key)
    {
        var result = await _systemParamRepository.GetByKeyAsync(key);
        if (result == null) return NotFound(new { message = "Không tìm thấy tham số này." });
        return Ok(result);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] SystemParam param)
    {
        if (key != param.ParamKey) return BadRequest(new { message = "Khóa tham số không khớp." });
        if (string.IsNullOrWhiteSpace(param.ParamValue))
        {
            return BadRequest(new { message = "Giá trị tham số cấu hình không được để trống." });
        }

        if (string.Equals(key, AuditLogDomainKey, StringComparison.Ordinal))
        {
            if (!TryNormalizeAuditLogDomain(param.ParamValue, out var normalizedDomain, out var errorMessage))
            {
                return BadRequest(new { message = errorMessage });
            }

            param.ParamValue = normalizedDomain;
        }

        var success = await _systemParamRepository.UpdateAsync(param);
        if (!success) return NotFound(new { message = "Không tìm thấy tham số hệ thống cần cập nhật." });
        return NoContent();
    }

    private bool TryNormalizeAuditLogDomain(string value, out string normalizedDomain, out string errorMessage)
    {
        normalizedDomain = string.Empty;
        errorMessage = "Domain dịch vụ nhật ký phải là URL tuyệt đối hợp lệ.";

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        var schemeAllowed = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || (_hostEnvironment.IsDevelopment() && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));
        if (!schemeAllowed)
        {
            errorMessage = _hostEnvironment.IsDevelopment()
                ? "Domain dịch vụ nhật ký chỉ cho phép URL https hoặc http trong môi trường Development."
                : "Domain dịch vụ nhật ký chỉ cho phép URL https.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            errorMessage = "Domain dịch vụ nhật ký không được chứa username hoặc password.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            errorMessage = "Domain dịch vụ nhật ký không được chứa fragment.";
            return false;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        normalizedDomain = uri.GetLeftPart(UriPartial.Authority) + path + uri.Query;
        return true;
    }
}
