// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Domain\Models\Menu.cs
namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class Menu
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public long? ParentId { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? PermissionCode { get; set; }
}
