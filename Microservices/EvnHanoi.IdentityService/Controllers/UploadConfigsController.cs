using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/upload-configs")]
public class UploadConfigsController : ControllerBase
{
    private readonly IUploadConfigRepository _uploadConfigRepository;

    public UploadConfigsController(IUploadConfigRepository uploadConfigRepository)
    {
        _uploadConfigRepository = uploadConfigRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _uploadConfigRepository.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _uploadConfigRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy cấu hình upload này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UploadConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Name) || string.IsNullOrWhiteSpace(config.AllowedExtensions))
        {
            var errors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(config.Name))
                errors.Add("name", "Tên cấu hình là bắt buộc.");
            if (string.IsNullOrWhiteSpace(config.AllowedExtensions))
                errors.Add("allowedExtensions", "Định dạng file được phép là bắt buộc.");

            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = errors
            });
        }

        var newId = await _uploadConfigRepository.CreateAsync(config);
        config.Id = newId;
        return CreatedAtAction(nameof(GetById), new { id = newId }, config);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] UploadConfig config)
    {
        if (id != config.Id)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "id", "ID không trùng khớp." } }
            });
        }

        if (string.IsNullOrWhiteSpace(config.Name) || string.IsNullOrWhiteSpace(config.AllowedExtensions))
        {
            var errors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(config.Name))
                errors.Add("name", "Tên cấu hình là bắt buộc.");
            if (string.IsNullOrWhiteSpace(config.AllowedExtensions))
                errors.Add("allowedExtensions", "Định dạng file được phép là bắt buộc.");

            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = errors
            });
        }

        var success = await _uploadConfigRepository.UpdateAsync(config);
        if (!success) return NotFound(new { message = "Không tìm thấy cấu hình upload cần chỉnh sửa." });
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _uploadConfigRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy cấu hình upload cần xóa." });
        return NoContent();
    }
}
