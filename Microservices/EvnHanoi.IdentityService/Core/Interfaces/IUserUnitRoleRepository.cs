// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Interfaces\IUserUnitRoleRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserUnitRoleRepository
{
    Task<IEnumerable<UserUnitRole>> GetUnitRolesByUserIdAsync(long userId);
    Task<bool> AssignUnitRolesAsync(long userId, IEnumerable<UserUnitRole> unitRoles);
    Task<IEnumerable<UserUnitRole>> GetUnitRolesByUserAndUnitAsync(long userId, long unitId);
}
