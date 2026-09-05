using Microsoft.AspNetCore.Http;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserGuideStorageService
{
    string BucketName { get; }
    Task<string> UploadGuideFileAsync(string roleName, IFormFile file, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType)> DownloadGuideFileAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteGuideFileAsync(string objectKey, CancellationToken cancellationToken = default);
}
