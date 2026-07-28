// Microservices/EvnHanoi.ReportService/Core/Interfaces/IReportRepository.cs
using EvnHanoi.ReportService.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Core.Interfaces
{
    public interface IReportRepository
    {
        // System Report Group
        Task<IEnumerable<ReportGroup>> GetReportGroupsAsync();
        Task<ReportGroup?> GetReportGroupByIdAsync(long id);
        Task<long> CreateReportGroupAsync(ReportGroup group, List<long> reportIds, List<long> unitIds);
        Task<bool> UpdateReportGroupAsync(ReportGroup group, List<long> reportIds, List<long> unitIds);
        Task<bool> DeleteReportGroupAsync(long id);
        Task<bool> LockReportGroupAsync(long id);
        Task<bool> UnlockReportGroupAsync(long id);

        // System Reports Lookup
        Task<IEnumerable<Report>> GetSystemReportsAsync();

        // Report Unit Publish
        Task<IEnumerable<ReportUnitPublish>> GetReportUnitPublishesAsync(long unitId);
        Task<bool> SaveReportUnitPublishAsync(long unitId, long reportId, int isPublish, List<long> roleIds, string? updatedBy);
        Task<IEnumerable<Report>> GetPublishedReportsForUserAsync(long unitId, List<string> roleCodes);
    }
}
