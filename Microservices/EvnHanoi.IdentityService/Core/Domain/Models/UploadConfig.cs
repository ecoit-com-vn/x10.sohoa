// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Domain\Models\UploadConfig.cs
namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UploadConfig
{
    public long Id { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string AllowedExtensions { get; set; } = string.Empty;
    public int MaxSizeMb { get; set; } = 10;
    public string? Description { get; set; }
}
