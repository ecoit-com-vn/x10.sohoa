namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UserGuide
{
    public long Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    /// <summary>Object key trong bucket MinIO chứa file hướng dẫn nhị phân.</summary>
    public string ObjectKey { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
