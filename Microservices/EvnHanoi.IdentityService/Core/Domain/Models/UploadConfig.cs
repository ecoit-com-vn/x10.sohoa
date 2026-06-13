using System.Text.Json.Serialization;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UploadConfig
{
    public long Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string AllowedExtensions { get; set; } = string.Empty;
    
    [JsonPropertyName("maxFileSizeMb")]
    public int MaxSizeMb { get; set; } = 10;
    
    public string? Description { get; set; }
    
    public long? OrganizationUnitId { get; set; }
    
    public string? OrganizationUnitName { get; set; } // Read-only property populated by LEFT JOIN in repositories
    
    public bool IsActive { get; set; } = true;
}
