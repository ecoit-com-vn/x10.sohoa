using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IRbacScopeAuthorizationService
{
    bool IsCentralAdmin(ClaimsPrincipal user);
    Task<IReadOnlySet<long>> GetManagedUnitIdsAsync(ClaimsPrincipal user);
    Task EnsureCanManagePermissionGroupsAsync(ClaimsPrincipal user);
    Task EnsureCanManageRoleAsync(ClaimsPrincipal user, Role role);
    Task EnsureCanAssignPermissionGroupsToRoleAsync(ClaimsPrincipal user, long roleId, IEnumerable<long> permissionGroupIds);
}
