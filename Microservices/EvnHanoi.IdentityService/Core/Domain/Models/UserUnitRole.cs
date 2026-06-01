// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Domain\Models\UserUnitRole.cs
namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UserUnitRole
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long UnitId { get; set; }
    public long RoleId { get; set; }
}
