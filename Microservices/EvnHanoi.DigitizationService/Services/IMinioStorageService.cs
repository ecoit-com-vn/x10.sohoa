using System.IO;
using System.Threading.Tasks;

namespace EvnHanoi.DigitizationService.Services
{
    public interface IMinioStorageService
    {
        Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType);
        Task<Stream> DownloadFileAsync(string bucketName, string objectName);
    }
}
