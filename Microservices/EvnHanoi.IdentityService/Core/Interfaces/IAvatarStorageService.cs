using Microsoft.AspNetCore.Http;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IAvatarStorageService
{
    string BucketName { get; }
    Task<string> UploadAvatarAsync(string userId, string? organizationUnitCode, IFormFile file, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType)> DownloadAvatarAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteAvatarAsync(string objectKey, CancellationToken cancellationToken = default);
}
