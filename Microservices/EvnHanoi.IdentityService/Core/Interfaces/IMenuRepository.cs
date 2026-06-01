// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Interfaces\IMenuRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IMenuRepository
{
    Task<IEnumerable<Menu>> GetAllAsync();
    Task<Menu?> GetByIdAsync(long id);
    Task<long> CreateAsync(Menu menu);
    Task<bool> UpdateAsync(Menu menu);
    Task<bool> DeleteAsync(long id);
    Task<IEnumerable<Menu>> GetMenusByUserPermissionsAsync(IEnumerable<string> permissionCodes);
}
