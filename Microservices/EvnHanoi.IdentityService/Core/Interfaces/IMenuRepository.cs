// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Interfaces\IMenuRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.DTOs;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IMenuRepository
{
    Task<IEnumerable<Menu>> GetAllAsync();
    Task<IEnumerable<Menu>> GetCoditionsAsync(string? keyword = null, bool? isActive = null);
    Task<Menu?> GetByIdAsync(long id);
    Task<long> CreateAsync(Menu menu);
    Task<bool> UpdateAsync(Menu menu);
    Task<bool> DeleteAsync(long id);
    Task<IEnumerable<Menu>> GetMenusByUserPermissionsAsync(IEnumerable<string> permissionCodes);

    /// <summary>Danh sách (RoleId, FuncId=Menu.Id) mà 1 vai trò được gán quyền sử dụng, suy ra qua
    /// ROLE_PERMISSION_GROUP → PERMISSION_GROUP_PERMISSION → PERMISSION.Code = APP_MENU.PermissionCode.</summary>
    Task<IEnumerable<RoleFunctionAssignmentDto>> GetRoleFunctionAssignmentsAsync();
}
