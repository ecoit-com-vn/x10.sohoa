using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IPermissionGroupRepository
{
    Task<IEnumerable<PermissionGroup>> GetAllAsync(string groupType, long? organizationUnitId = null);
    Task<(IEnumerable<PermissionGroup> Items, int TotalCount)> GetPagedAsync(
        string groupType, int page, int pageSize, string? keyword = null, long? organizationUnitId = null);
    Task<PermissionGroup?> GetByIdAsync(long id, string groupType);
    Task<long> CreateAsync(PermissionGroup group);
    Task<bool> UpdateAsync(PermissionGroup group);
    Task<bool> DeleteAsync(long id, string groupType);
    Task<IEnumerable<string>> GetPermissionCodesByGroupIdAsync(long permissionGroupId);
    Task<bool> AssignPermissionsToGroupAsync(long permissionGroupId, IEnumerable<string> permissionCodes);
    Task<IEnumerable<long>> GetPermissionGroupIdsByRoleIdAsync(long roleId);
    Task<IEnumerable<PermissionGroup>> GetPermissionGroupsByRoleIdAsync(long roleId);
    Task<IEnumerable<long>> GetOrganizationUnitIdsByGroupIdAsync(long permissionGroupId);
    Task AssignOrganizationUnitsAsync(long permissionGroupId, IEnumerable<long> organizationUnitIds);
}
