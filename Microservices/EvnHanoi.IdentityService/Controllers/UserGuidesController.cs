using System;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/user-guides")]
public class UserGuidesController : ControllerBase
{
    private readonly IUserGuideRepository _userGuideRepository;
    private readonly IUserGuideStorageService _userGuideStorageService;

    public UserGuidesController(
        IUserGuideRepository userGuideRepository,
        IUserGuideStorageService userGuideStorageService)
    {
        _userGuideRepository = userGuideRepository;
        _userGuideStorageService = userGuideStorageService;
    }

    private string? CurrentUser =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _userGuideRepository.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _userGuideRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy hướng dẫn sử dụng này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] string roleName, [FromForm] IFormFile? file)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return BadRequest(new { message = "Tên vai trò là bắt buộc." });
        }
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng tải lên file hướng dẫn." });
        }

        string objectKey;
        try
        {
            objectKey = await _userGuideStorageService.UploadGuideFileAsync(roleName, file);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var guide = new UserGuide
        {
            RoleName = roleName.Trim(),
            FileName = file.FileName,
            ObjectKey = objectKey,
            FileSize = file.Length,
            ContentType = file.ContentType,
            CreatedBy = CurrentUser
        };

        var newId = await _userGuideRepository.CreateAsync(guide);
        guide.Id = newId;
        return CreatedAtAction(nameof(GetById), new { id = newId }, guide);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromForm] string roleName, [FromForm] IFormFile? file)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return BadRequest(new { message = "Tên vai trò là bắt buộc." });
        }

        var existing = await _userGuideRepository.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = "Không tìm thấy hướng dẫn sử dụng cần chỉnh sửa." });
        }

        var objectKey = existing.ObjectKey;
        var fileName = existing.FileName;
        var fileSize = existing.FileSize;
        var contentType = existing.ContentType;
        var previousObjectKey = existing.ObjectKey;

        if (file is { Length: > 0 })
        {
            try
            {
                objectKey = await _userGuideStorageService.UploadGuideFileAsync(roleName, file);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            fileName = file.FileName;
            fileSize = file.Length;
            contentType = file.ContentType;
        }

        var guide = new UserGuide
        {
            Id = id,
            RoleName = roleName.Trim(),
            FileName = fileName,
            ObjectKey = objectKey,
            FileSize = fileSize,
            ContentType = contentType,
            UpdatedBy = CurrentUser
        };

        var success = await _userGuideRepository.UpdateAsync(guide);
        if (!success) return NotFound(new { message = "Không tìm thấy hướng dẫn sử dụng cần chỉnh sửa." });

        // Chỉ xóa file cũ sau khi đã cập nhật DB thành công, tránh mất file khi update thất bại giữa chừng.
        if (!string.Equals(previousObjectKey, objectKey, StringComparison.Ordinal))
        {
            await _userGuideStorageService.DeleteGuideFileAsync(previousObjectKey);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var existing = await _userGuideRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy hướng dẫn sử dụng cần xóa." });

        var success = await _userGuideRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy hướng dẫn sử dụng cần xóa." });

        await _userGuideStorageService.DeleteGuideFileAsync(existing.ObjectKey);
        return NoContent();
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(long id)
    {
        var existing = await _userGuideRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy hướng dẫn sử dụng này." });

        var (stream, contentType) = await _userGuideStorageService.DownloadGuideFileAsync(existing.ObjectKey);
        return File(stream, contentType, existing.FileName);
    }
}
