using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IPermissionRepository
{
    Task<IEnumerable<Permission>> GetAllPermissionsAsync();
    Task<Permission?> GetPermissionByIdAsync(string id);
    Task<string> CreatePermissionAsync(Permission permission, IEnumerable<PermissionDetail> details);
    Task<bool> UpdatePermissionAsync(Permission permission, IEnumerable<PermissionDetail> details);
    Task<bool> DeletePermissionAsync(string id);
    
    Task<bool> AssignPermissionsToUserAsync(string userId, IEnumerable<string> permissionIds);
    Task<bool> AssignPermissionsToUserGroupAsync(long userGroupId, IEnumerable<string> permissionIds);
    
    Task<IEnumerable<PermissionDetail>> GetAllowedActionsForUserAsync(string userId);
    Task<IEnumerable<string>> GetPermissionsByUserIdAsync(string userId);
    Task<IEnumerable<string>> GetPermissionsByUserGroupIdAsync(long userGroupId);
}
