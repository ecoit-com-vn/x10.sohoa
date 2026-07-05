namespace EvnHanoi.NotificationService.Services;

public interface IMinioOcrTextReader
{
    /// <summary>
    /// Đọc và ghép nội dung các file {basePath}_page_{n}.md từ MinIO.
    /// </summary>
    Task<string> ReadConcatenatedMarkdownAsync(
        string bucketName,
        string pdfFilePath,
        int totalPagesHint,
        CancellationToken cancellationToken = default);
}
