using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API Quản lý phân bổ nhập liệu thư mục
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/folder-allocations")]
public class FolderAllocationController : ControllerBase
{
    private readonly IFolderAllocationService _folderAllocationService;

    public FolderAllocationController(IFolderAllocationService folderAllocationService)
    {
        _folderAllocationService = folderAllocationService ?? throw new ArgumentNullException(nameof(folderAllocationService));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? User.Identity?.Name ?? "system";

    private string UserName => User.FindFirst("preferred_username")?.Value
                             ?? User.FindFirst(ClaimTypes.Name)?.Value
                             ?? User.Identity?.Name ?? "system";

    private long GetUserUnitId()
    {
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(unitIdClaim, out var unitId) ? unitId : 0;
    }

    /// <summary>
    /// Lấy danh sách phân bổ phân trang
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null)
    {
        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var (items, totalCount) = await _folderAllocationService.GetPagedAsync(page, page_size, keyword, status, userUnitId);
        return Ok(new
        {
            items,
            total_count = totalCount,
            page,
            page_size
        });
    }

    /// <summary>
    /// Lấy chi tiết thông tin phân bổ
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var allocation = await _folderAllocationService.GetByIdAsync(id, userUnitId);
        if (allocation == null)
            return NotFound(new { message = "Không tìm thấy thông tin phân bổ." });

        return Ok(allocation);
    }

    /// <summary>
    /// Tạo mới phân bổ
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFolderAllocationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        try
        {
            var id = await _folderAllocationService.CreateAsync(request, UserName, userUnitId);
            return Created($"api/v1/folder-allocations/{id}", new { id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật thông tin phân bổ
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderAllocationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        try
        {
            var success = await _folderAllocationService.UpdateAsync(id, request, UserName, userUnitId);
            if (!success)
                return BadRequest(new { message = "Không thể cập nhật thông tin phân bổ." });

            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Thu hồi phân bổ (Status -> Revoked)
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        try
        {
            var success = await _folderAllocationService.RevokeAsync(id, UserName, userUnitId);
            if (!success)
                return BadRequest(new { message = "Không thể thu hồi phân bổ." });

            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa phân bổ (Soft Delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        try
        {
            var success = await _folderAllocationService.DeleteAsync(id, UserName, userUnitId);
            if (!success)
                return BadRequest(new { message = "Không thể xóa phân bổ." });

            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách người dùng để lookup dropdown
    /// </summary>
    [HttpGet("lookup/users")]
    public async Task<IActionResult> GetUsersLookup()
    {
        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var users = await _folderAllocationService.GetUsersLookupAsync(userUnitId);
        return Ok(users);
    }

    /// <summary>
    /// Lấy danh sách thư mục để lookup dropdown/tree
    /// </summary>
    [HttpGet("lookup/folders")]
    public async Task<IActionResult> GetFoldersLookup()
    {
        var userUnitId = GetUserUnitId();
        if (userUnitId == 0)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var folders = await _folderAllocationService.GetFoldersLookupAsync(userUnitId);
        return Ok(folders);
    }

    /// <summary>
    /// Lấy danh sách thư mục được phân bổ của chính người dùng hiện tại (kế thừa thư mục con)
    /// </summary>
    [HttpGet("my-folders")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetMyFolders()
    {
        var userId = UserId;
        if (string.IsNullOrEmpty(userId) || userId == "system")
            return Unauthorized(new { message = "Không thể xác định danh tính người dùng" });

        var folders = await _folderAllocationService.GetMyFoldersAsync(userId);
        return Ok(folders);
    }
}
