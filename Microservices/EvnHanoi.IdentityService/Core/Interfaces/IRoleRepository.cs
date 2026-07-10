using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IRoleRepository
{
    Task<IEnumerable<Role>> GetAllAsync(int? scopeTypeId = null, long? organizationUnitId = null, bool includeDescendants = false);
    Task<(IEnumerable<Role> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? keyword = null, int? scopeTypeId = null, long? organizationUnitId = null, bool includeDescendants = false);
    Task<Role?> GetByIdAsync(long id);
    Task<long> CreateAsync(Role role);
    Task<bool> UpdateAsync(Role role);
    Task<bool> DeleteAsync(long id);
    Task<bool> AssignPermissionGroupsAsync(long roleId, IEnumerable<long> permissionGroupIds);
    Task<(IEnumerable<RoleAssignedUserListItem> Items, int TotalCount)> GetUsersByRoleIdPagedAsync(
        long roleId, int page, int pageSize, string? keyword = null);
}
