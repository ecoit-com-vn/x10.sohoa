using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IBorrowRecordRepository
    {
        Task<IEnumerable<BorrowRecord>> GetAllAsync();
        Task<(IEnumerable<BorrowRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, BorrowState? state = null);
        Task<BorrowRecord?> GetByIdAsync(Guid id);
        Task<bool> CreateAsync(BorrowRecord record);
        Task<bool> UpdateAsync(BorrowRecord record);
        Task<IEnumerable<BorrowRecord>> GetSummaryByIdsAsync(IEnumerable<Guid> ids);
    }
}
