using System.Text.Json.Serialization;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class ExternalApiKey
{
    public long Id { get; set; }
    public string KeyName { get; set; } = string.Empty;

    [JsonIgnore]
    public string KeyHash { get; set; } = string.Empty;

    [JsonIgnore]
    public string? EncryptedKey { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? Note { get; set; }
}

public class CreateExternalApiKeyRequest
{
    public string KeyName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public string? Note { get; set; }
}

public class UpdateExternalApiKeyRequest
{
    public string KeyName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Note { get; set; }
}
