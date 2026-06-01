using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/system-params")]
public class SystemParamsController : ControllerBase
{
    private readonly ISystemParamRepository _systemParamRepository;

    public SystemParamsController(ISystemParamRepository systemParamRepository)
    {
        _systemParamRepository = systemParamRepository;
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

        var success = await _systemParamRepository.UpdateAsync(param);
        if (!success) return NotFound(new { message = "Không tìm thấy tham số hệ thống cần cập nhật." });
        return NoContent();
    }
}
