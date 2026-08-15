using System.Security.Claims;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Security;
using EvnHanoi.SyncService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// Cấu hình URL + header cho 9 API PMIS (đúng số lượng cố định theo tài liệu tích hợp — chỉ sửa,
/// không thêm/xoá dòng). Giải quyết việc URL API PMIS còn để trống trong tài liệu: admin tự điền
/// khi PMIS cung cấp, không cần build/deploy lại.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/sync/endpoint-config")]
public class PmisEndpointConfigController : ControllerBase
{
    private readonly IPmisEndpointConfigRepository _repository;
    private readonly IPmisHeaderValueProtector _protector;
    private readonly IPmisEndpointConfigProvider _provider;

    public PmisEndpointConfigController(
        IPmisEndpointConfigRepository repository,
        IPmisHeaderValueProtector protector,
        IPmisEndpointConfigProvider provider)
    {
        _repository = repository;
        _protector = protector;
        _provider = provider;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _repository.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{apiCode}/headers")]
    public async Task<IActionResult> GetHeaders(string apiCode)
    {
        var config = await _repository.GetByApiCodeAsync(apiCode);
        if (config == null) return NotFound(new { message = "Không tìm thấy API PMIS cần xem." });

        var headers = await _repository.GetHeadersAsync(config.Id);
        var dtos = headers.Select(h => new PmisApiEndpointHeaderDto
        {
            Id = h.Id,
            HeaderKey = h.HeaderKey,
            IsSecret = h.IsSecret,
            HeaderValue = h.IsSecret ? "••••••" : h.HeaderValue
        });
        return Ok(dtos);
    }

    [HttpPut("{apiCode}")]
    public async Task<IActionResult> Update(string apiCode, [FromBody] UpdatePmisApiEndpointConfigRequest request)
    {
        var existing = await _repository.GetByApiCodeAsync(apiCode);
        if (existing == null) return NotFound(new { message = "Không tìm thấy API PMIS cần cập nhật." });

        var modifiedBy = CurrentUserName();
        var updated = await _repository.UpdateAsync(apiCode, request, modifiedBy);
        if (!updated)
        {
            return Conflict(new
            {
                message = "Dữ liệu đã được người khác cập nhật, vui lòng tải lại trang trước khi lưu tiếp."
            });
        }

        _provider.Invalidate(apiCode);
        return NoContent();
    }

    [HttpPut("{apiCode}/headers")]
    public async Task<IActionResult> ReplaceHeaders(string apiCode, [FromBody] ReplacePmisApiEndpointHeadersRequest request)
    {
        var config = await _repository.GetByApiCodeAsync(apiCode);
        if (config == null) return NotFound(new { message = "Không tìm thấy API PMIS cần cập nhật header." });

        if (request.Headers.Any(h => string.IsNullOrWhiteSpace(h.HeaderKey)))
            return BadRequest(new { message = "Tên header không được để trống." });

        var existingHeaders = (await _repository.GetHeadersAsync(config.Id)).ToDictionary(h => h.Id);
        var modifiedBy = CurrentUserName();

        var toSave = request.Headers.Select(dto =>
        {
            // Header bí mật đã tồn tại + không nhập giá trị mới -> giữ nguyên giá trị đã mã hoá cũ,
            // tránh phải round-trip giá trị bí mật đã lưu ra ngoài client.
            if (dto.IsSecret && string.IsNullOrEmpty(dto.HeaderValue) &&
                dto.Id != null && existingHeaders.TryGetValue(dto.Id, out var existing))
            {
                return new PmisApiEndpointHeader
                {
                    Id = dto.Id,
                    EndpointConfigId = config.Id,
                    HeaderKey = dto.HeaderKey.Trim(),
                    HeaderValue = existing.HeaderValue,
                    IsSecret = true
                };
            }

            return new PmisApiEndpointHeader
            {
                Id = dto.Id ?? string.Empty,
                EndpointConfigId = config.Id,
                HeaderKey = dto.HeaderKey.Trim(),
                HeaderValue = dto.IsSecret && !string.IsNullOrEmpty(dto.HeaderValue)
                    ? _protector.Protect(dto.HeaderValue)
                    : dto.HeaderValue,
                IsSecret = dto.IsSecret
            };
        }).ToList();

        await _repository.ReplaceHeadersAsync(config.Id, toSave, modifiedBy);
        _provider.Invalidate(apiCode);
        return NoContent();
    }

    private string? CurrentUserName() =>
        User.FindFirstValue("full_name") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
