// Microservices/EvnHanoi.ReportService/Core/Interfaces/IReportRepository.cs
using EvnHanoi.ReportService.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Core.Interfaces
{
    public interface IReportRepository
    {
        // Report Group
        Task<IEnumerable<ReportGroup>> GetReportGroupsAsync();
        Task<ReportGroup?> GetReportGroupByIdAsync(long id);
        Task<long> CreateReportGroupAsync(ReportGroup group);
        Task<bool> UpdateReportGroupAsync(ReportGroup group);
        Task<bool> DeleteReportGroupAsync(long id);

        // Dynamic Report
        Task<IEnumerable<DynamicReport>> GetDynamicReportsByGroupIdAsync(long groupId);
        Task<DynamicReport?> GetDynamicReportByIdAsync(long id);
        Task<long> CreateDynamicReportAsync(DynamicReport report);
        Task<bool> UpdateDynamicReportAsync(DynamicReport report);
        Task<bool> DeleteDynamicReportAsync(long id);

        // Execute SQL Query
        Task<IEnumerable<IDictionary<string, object>>> ExecuteDynamicQueryAsync(string sql, Dictionary<string, object>? parameters);
    }
}
