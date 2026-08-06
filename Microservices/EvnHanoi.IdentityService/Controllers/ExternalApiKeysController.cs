using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/v1/external-api-keys")]
public class ExternalApiKeysController : ControllerBase
{
    private readonly IExternalApiKeyRepository _externalApiKeyRepository;
    private readonly IExternalApiKeyProtector _externalApiKeyProtector;
    private readonly IExternalApiCallLogRepository _externalApiCallLogRepository;

    public ExternalApiKeysController(
        IExternalApiKeyRepository externalApiKeyRepository,
        IExternalApiKeyProtector externalApiKeyProtector,
        IExternalApiCallLogRepository externalApiCallLogRepository)
    {
        _externalApiKeyRepository = externalApiKeyRepository;
        _externalApiKeyProtector = externalApiKeyProtector;
        _externalApiCallLogRepository = externalApiCallLogRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _externalApiKeyRepository.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var item = await _externalApiKeyRepository.GetByIdAsync(id);
        if (item == null) return NotFound(new { message = "Không tìm thấy private key." });

        return Ok(new { item, privateKey = _externalApiKeyProtector.Unprotect(item.EncryptedKey) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExternalApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.KeyName))
            return BadRequest(new { message = "Bạn chưa nhập KeyName." });

        if (request.ExpiresAt.HasValue && request.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { message = "Thời điểm hết hạn chưa đúng." });

        var privateKey = GeneratePrivateKey();
        var apiKey = new ExternalApiKey
        {
            KeyName = request.KeyName.Trim(),
            KeyHash = ComputeSha256(privateKey),
            EncryptedKey = _externalApiKeyProtector.Protect(privateKey),
            IsActive = request.IsActive,
            ExpiresAt = request.ExpiresAt,
            CreatedBy = User.FindFirstValue("full_name")
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "SYSTEM",
            RevokedAt = request.IsActive ? null : DateTime.UtcNow,
            Note = request.Note?.Trim()
        };

        var id = await _externalApiKeyRepository.CreateAsync(apiKey);
        apiKey.Id = id;

        return CreatedAtAction(nameof(GetById), new { id }, new { item = apiKey, privateKey });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateExternalApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.KeyName))
            return BadRequest(new { message = "Bạn chưa nhập KeyName." });

        if (request.ExpiresAt.HasValue && request.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { message = "Thời điểm hết hạn chưa đúng." });

        var existing = await _externalApiKeyRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Khong tim thay private key can cap nhat." });

        existing.KeyName = request.KeyName.Trim();
        existing.IsActive = request.IsActive;
        existing.ExpiresAt = request.ExpiresAt;
        existing.Note = request.Note?.Trim();
        existing.RevokedAt = request.IsActive ? null : DateTime.UtcNow;

        await _externalApiKeyRepository.UpdateAsync(existing);
        return NoContent();
    }

    [HttpGet("call-logs")]
    public async Task<IActionResult> GetAllCallLogs(
        [FromQuery] string? keyName = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            return BadRequest(new { message = "fromDate không được lớn hơn toDate." });

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var filter = new ExternalApiCallLogFilter
        {
            KeyName = keyName,
            DateFrom = fromDate,
            DateTo = toDate
        };

        var (items, totalCount) = await _externalApiCallLogRepository.GetAllAsync(filter, page, pageSize);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:long}/call-logs")]
    public async Task<IActionResult> GetCallLogs(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var existing = await _externalApiKeyRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy private key." });

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, totalCount) = await _externalApiCallLogRepository.GetByApiKeyIdAsync(id, page, pageSize);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpPost("{id:long}/regenerate")]
    public async Task<IActionResult> Regenerate(long id)
    {
        var existing = await _externalApiKeyRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy private key cần cập nhật." });

        var privateKey = GeneratePrivateKey();
        var success = await _externalApiKeyRepository.UpdateKeyValueAsync(
            id,
            ComputeSha256(privateKey),
            _externalApiKeyProtector.Protect(privateKey));

        if (!success) return NotFound(new { message = "Không thể cập nhật private key." });

        existing.EncryptedKey = null;
        return Ok(new { item = existing, privateKey });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _externalApiKeyRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy private key cần xóa." });

        return NoContent();
    }

    private static string GeneratePrivateKey()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return $"ecoit_{Convert.ToBase64String(randomBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
