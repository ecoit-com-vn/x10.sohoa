using System;

using System.Collections.Generic;

using System.Linq;

using System.Security.Claims;

using System.Threading.Tasks;

using EvnHanoi.IdentityService.Core.Domain.Models;

using EvnHanoi.IdentityService.Core.Interfaces;

using EvnHanoi.IdentityService.Infrastructure.Security;

using EvnHanoi.Infrastructure.Audit;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;



namespace EvnHanoi.IdentityService.Controllers;



[ApiController]

[Route("api/v1/unit-permission-groups")]

[Authorize]

public class UnitPermissionGroupsController : ControllerBase

{

    private readonly IPermissionGroupRepository _permissionGroupRepository;

    private readonly IRbacScopeAuthorizationService _rbacScope;



    public UnitPermissionGroupsController(

        IPermissionGroupRepository permissionGroupRepository,

        IRbacScopeAuthorizationService rbacScope)

    {

        _permissionGroupRepository = permissionGroupRepository;

        _rbacScope = rbacScope;

    }



    [HttpGet("lookup")]

    [BypassDynamicPermission]

    public async Task<IActionResult> GetLookup([FromQuery] long? organizationUnitId = null)

    {

        long? filterUnitId = organizationUnitId;

        if (!_rbacScope.IsCentralAdmin(User))

        {

            var managed = await _rbacScope.GetManagedUnitIdsAsync(User);

            if (managed.Count == 0) return Ok(Array.Empty<object>());

            if (filterUnitId.HasValue && !managed.Contains(filterUnitId.Value))

            {

                return StatusCode(403, new { message = "Bạn không có quyền xem nhóm quyền đơn vị này." });

            }



            if (!filterUnitId.HasValue)

            {

                // Lấy nhóm có ít nhất một đơn vị trong phạm vi quản lý (tránh N+1 theo từng ĐV)

                var allUnitGroups = await _permissionGroupRepository.GetAllAsync(PermissionGroupTypes.Unit);

                var filtered = allUnitGroups

                    .Where(g => g.OrganizationUnitIds.Any(uid => managed.Contains(uid)))

                    .Select(ToLookupDto);

                return Ok(filtered);

            }

        }



        var items = await _permissionGroupRepository.GetAllAsync(PermissionGroupTypes.Unit, filterUnitId);

        return Ok(items.Select(ToLookupDto));

    }



    [HttpGet]

    public async Task<IActionResult> GetAll(

        [FromQuery] int page = 1,

        [FromQuery] int pageSize = 10,

        [FromQuery] string? keyword = null,

        [FromQuery] long? organizationUnitId = null)

    {

        if (!_rbacScope.IsCentralAdmin(User))

        {

            return StatusCode(403, new { message = "Chỉ quản trị đơn vị tổng mới được xem danh sách quản trị nhóm quyền đơn vị." });

        }



        var (items, totalCount) = await _permissionGroupRepository.GetPagedAsync(

            PermissionGroupTypes.Unit, page, pageSize, keyword, organizationUnitId);

        return Ok(new { items, totalCount, page, pageSize });

    }



    [HttpGet("{id}")]

    public async Task<IActionResult> GetById(long id)

    {

        if (!_rbacScope.IsCentralAdmin(User))

        {

            return StatusCode(403, new { message = "Chỉ quản trị đơn vị tổng mới được xem chi tiết nhóm quyền đơn vị." });

        }



        var result = await _permissionGroupRepository.GetByIdAsync(id, PermissionGroupTypes.Unit);

        if (result == null) return NotFound(new { message = "Không tìm thấy nhóm quyền đơn vị." });

        return Ok(result);

    }



    [HttpPost]

    public async Task<IActionResult> Create([FromBody] PermissionGroup group)

    {

        try

        {

            group.ScopeTypeId = RoleScopeTypes.UNIT.Id;

            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);

        }

        catch (UnauthorizedAccessException ex)

        {

            return StatusCode(403, new { message = ex.Message });

        }



        if (string.IsNullOrWhiteSpace(group.Code) || string.IsNullOrWhiteSpace(group.Name))

        {

            return BadRequest(new { message = "Mã và Tên nhóm quyền là bắt buộc." });

        }



        NormalizeIncomingUnits(group);

        if (group.OrganizationUnitIds.Count == 0)

        {

            return BadRequest(new { message = "Nhóm quyền đơn vị phải chọn ít nhất một đơn vị.", errors = new { organizationUnitIds = "Đơn vị là bắt buộc" } });

        }



        group.GroupType = PermissionGroupTypes.Unit;

        group.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "SYSTEM";

        var newId = await _permissionGroupRepository.CreateAsync(group);

        group.Id = newId;

        HttpContext.SetAudit(newId.ToString(), group.Code, $"Tạo nhóm quyền ĐV {group.Code}", "PERMISSION_GROUP", AuditActions.Create);

        return CreatedAtAction(nameof(GetById), new { id = newId }, group);

    }



    [HttpPut("{id}")]

    public async Task<IActionResult> Update(long id, [FromBody] PermissionGroup group)

    {

        try

        {

            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);

        }

        catch (UnauthorizedAccessException ex)

        {

            return StatusCode(403, new { message = ex.Message });

        }



        if (id != group.Id) return BadRequest(new { message = "ID không trùng khớp." });



        NormalizeIncomingUnits(group);

        if (group.OrganizationUnitIds.Count == 0)

        {

            return BadRequest(new { message = "Nhóm quyền đơn vị phải chọn ít nhất một đơn vị.", errors = new { organizationUnitIds = "Đơn vị là bắt buộc" } });

        }



        group.GroupType = PermissionGroupTypes.Unit;

        var success = await _permissionGroupRepository.UpdateAsync(group);

        if (!success) return NotFound(new { message = "Không tìm thấy nhóm quyền cần chỉnh sửa." });

        HttpContext.SetAudit(id.ToString(), group.Code, $"Cập nhật nhóm quyền ĐV {group.Code}", "PERMISSION_GROUP", AuditActions.Update);

        return NoContent();

    }



    [HttpDelete("{id}")]

    public async Task<IActionResult> Delete(long id)

    {

        try

        {

            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);

        }

        catch (UnauthorizedAccessException ex)

        {

            return StatusCode(403, new { message = ex.Message });

        }



        var group = await _permissionGroupRepository.GetByIdAsync(id, PermissionGroupTypes.Unit);

        var success = await _permissionGroupRepository.DeleteAsync(id, PermissionGroupTypes.Unit);

        if (!success) return NotFound(new { message = "Không tìm thấy nhóm quyền cần xóa." });

        HttpContext.SetAudit(id.ToString(), group?.Code, $"Xóa nhóm quyền ĐV {group?.Code}", "PERMISSION_GROUP", AuditActions.Delete);

        return NoContent();

    }



    [HttpGet("{id}/permissions")]

    public async Task<IActionResult> GetPermissions(long id)

    {

        if (!_rbacScope.IsCentralAdmin(User))

        {

            return StatusCode(403, new { message = "Chỉ quản trị đơn vị tổng mới được xem quyền của nhóm quyền đơn vị." });

        }



        var permissions = await _permissionGroupRepository.GetPermissionCodesByGroupIdAsync(id);

        return Ok(permissions);

    }



    [HttpPost("{id}/permissions")]

    public async Task<IActionResult> AssignPermissions(long id, [FromBody] List<string> permissionCodes)

    {

        try

        {

            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);

        }

        catch (UnauthorizedAccessException ex)

        {

            return StatusCode(403, new { message = ex.Message });

        }



        if (permissionCodes == null) return BadRequest(new { message = "Danh sách quyền không hợp lệ." });

        var success = await _permissionGroupRepository.AssignPermissionsToGroupAsync(id, permissionCodes);

        if (!success) return BadRequest(new { message = "Gán quyền không thành công." });

        HttpContext.SetAudit(id.ToString(), null, $"Gán {permissionCodes.Count} quyền cho nhóm ĐV {id}", "PERMISSION_GROUP", AuditActions.Manage);

        return Ok(new { message = "Gán quyền cho nhóm quyền đơn vị thành công." });

    }



    private static void NormalizeIncomingUnits(PermissionGroup group)

    {

        group.OrganizationUnitIds ??= new List<long>();

        if (group.OrganizationUnitIds.Count == 0 && group.OrganizationUnitId.HasValue)

        {

            group.OrganizationUnitIds.Add(group.OrganizationUnitId.Value);

        }



        group.OrganizationUnitIds = group.OrganizationUnitIds

            .Where(id => id > 0)

            .Distinct()

            .ToList();



        group.OrganizationUnitId = group.OrganizationUnitIds.Count > 0 ? group.OrganizationUnitIds[0] : null;

    }



    private static object ToLookupDto(PermissionGroup g) => new

    {

        g.Id,

        g.Code,

        g.Name,

        g.OrganizationUnitId,

        g.OrganizationUnitName,

        g.OrganizationUnitIds,

        g.OrganizationUnitNames

    };

}

