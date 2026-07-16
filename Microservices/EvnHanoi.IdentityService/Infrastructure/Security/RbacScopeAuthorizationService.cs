using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Security;

public class RbacScopeAuthorizationService : IRbacScopeAuthorizationService
{
    private readonly IDbConnection _connection;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionGroupRepository _permissionGroupRepository;
    private readonly IOrganizationUnitRepository _organizationUnitRepository;

    public RbacScopeAuthorizationService(
        IDbConnection connection,
        IRoleRepository roleRepository,
        IPermissionGroupRepository permissionGroupRepository,
        IOrganizationUnitRepository organizationUnitRepository)
    {
        _connection = connection;
        _roleRepository = roleRepository;
        _permissionGroupRepository = permissionGroupRepository;
        _organizationUnitRepository = organizationUnitRepository;
    }

    public bool IsCentralAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole("ADMIN")
            || user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN")
            || user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "SUPER_ADMIN");
    }

    public async Task<IReadOnlySet<long>> GetManagedUnitIdsAsync(ClaimsPrincipal user)
    {
        if (IsCentralAdmin(user))
        {
            var all = await _organizationUnitRepository.GetAllAsync();
            return all.Select(u => u.Id).ToHashSet();
        }

        var unitIdClaim = user.FindFirst("unit_id")?.Value;
        if (string.IsNullOrEmpty(unitIdClaim) || !long.TryParse(unitIdClaim, out var startUnitId))
        {
            return new HashSet<long>();
        }

        var units = await _organizationUnitRepository.GetOrganizationUnitsHierarchicalAsync(startUnitId);
        return units.Select(u => u.Id).ToHashSet();
    }

    public Task EnsureCanManagePermissionGroupsAsync(ClaimsPrincipal user)
    {
        if (!IsCentralAdmin(user))
        {
            throw new UnauthorizedAccessException("Chỉ quản trị đơn vị tổng mới được quản lý nhóm quyền.");
        }

        return Task.CompletedTask;
    }

    public async Task EnsureCanManageRoleAsync(ClaimsPrincipal user, Role role)
    {
        if (IsCentralAdmin(user))
        {
            return;
        }

        if (role.ScopeTypeId != RoleScopeTypes.UNIT.Id)
        {
            throw new UnauthorizedAccessException("Quản trị đơn vị chỉ được quản lý vai trò trong phạm vi đơn vị.");
        }

        if (!role.OrganizationUnitId.HasValue)
        {
            throw new UnauthorizedAccessException("Vai trò đơn vị phải gắn với một đơn vị.");
        }

        var managed = await GetManagedUnitIdsAsync(user);
        if (!managed.Contains(role.OrganizationUnitId.Value))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý vai trò của đơn vị này.");
        }
    }

    public async Task EnsureCanAssignPermissionGroupsToRoleAsync(
        ClaimsPrincipal user, long roleId, IEnumerable<long> permissionGroupIds)
    {
        var role = await _roleRepository.GetByIdAsync(roleId)
            ?? throw new KeyNotFoundException("Không tìm thấy vai trò.");

        await EnsureCanManageRoleAsync(user, role);

        var ids = permissionGroupIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var managedUnits = await GetManagedUnitIdsAsync(user);
        foreach (var groupId in ids)
        {
            var group = await _permissionGroupRepository.GetByIdAsync(groupId, PermissionGroupTypes.System)
                ?? await _permissionGroupRepository.GetByIdAsync(groupId, PermissionGroupTypes.Unit);

            if (group == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy nhóm quyền {groupId}.");
            }

            if (string.Equals(group.GroupType, PermissionGroupTypes.System, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsCentralAdmin(user))
                {
                    throw new UnauthorizedAccessException("Quản trị đơn vị không được gắn nhóm quyền hệ thống vào vai trò.");
                }

                continue;
            }

            var groupUnitIds = group.OrganizationUnitIds?.Count > 0
                ? group.OrganizationUnitIds
                : (group.OrganizationUnitId.HasValue
                    ? new List<long> { group.OrganizationUnitId.Value }
                    : new List<long>());

            if (groupUnitIds.Count == 0)
            {
                throw new UnauthorizedAccessException("Nhóm quyền đơn vị chưa được gắn đơn vị.");
            }

            // Vai trò UNIT: nhóm phải chứa đơn vị của vai trò trong mapping
            if (role.ScopeTypeId == RoleScopeTypes.UNIT.Id && role.OrganizationUnitId.HasValue)
            {
                if (!groupUnitIds.Contains(role.OrganizationUnitId.Value))
                {
                    throw new UnauthorizedAccessException(
                        "Nhóm quyền đơn vị phải bao gồm đúng đơn vị của vai trò.");
                }
            }

            if (!groupUnitIds.Any(managedUnits.Contains))
            {
                throw new UnauthorizedAccessException("Bạn không được gắn nhóm quyền đơn vị ngoài phạm vi quản lý.");
            }
        }
    }
}
