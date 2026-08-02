using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Models.Dto;
using EvnHanoi.DigitizationService.Models.OcrModule;

namespace EvnHanoi.DigitizationService.Repositories
{
    public interface IOcrTrainingDataRepository
    {
        Task<long> CreateAsync(OcrTrainingData data);
        Task<OcrTrainingData?> GetByIdAsync(long id);
        Task<PagedResult<OcrTrainingDataSummaryDto>> GetPagedAsync(
            int page, int pageSize,
            string? documentType = null,
            string? trainingStatus = null,
            string? keyword = null);
        Task UpdateAsync(OcrTrainingData data);
        Task UpdateLabelAsync(long id, string? labelText, string documentType, string trainingStatus, decimal? qualityScore, string? notes);
        Task VerifyAsync(long id, bool isVerified, string verifiedBy, string? notes);
        Task DeleteAsync(long id);
        Task<int> GetCountByStatusAsync(string status);

        // Yêu cầu 91 (mở rộng) — gán nhãn theo từng trường, liên kết job huấn luyện lại, export theo dataset version.
        Task UpdateFieldLabelsAsync(long id, string fieldLabelsJson);
        Task<string> CreateRetrainJobAsync(OcrTrainingRetrainJob job);
        Task LinkRetrainJobAsync(long trainingDataId, string retrainJobId, string? datasetVersion);
        Task<string> CreateDatasetVersionAsync(OcrTrainingDatasetVersion version);
        Task<List<OcrTrainingDatasetVersion>> GetDatasetVersionsAsync();
        Task<List<OcrTrainingData>> GetForExportAsync(string? datasetVersion);
    }
}
