// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Domain\Models\UserGroup.cs
namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UserGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
