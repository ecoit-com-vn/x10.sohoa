using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;

namespace EvnHanoi.DigitizationService.Repositories
{
    public interface IDigitizationTaskRepository
    {
        Task<Guid> CreateAsync(DigitizationTask task);
        Task<DigitizationTask?> GetByIdAsync(Guid id);
        Task<IEnumerable<DigitizationTask>> GetByUserIdAsync(string userId);
        Task<IEnumerable<DigitizationTask>> GetAllAsync();
        Task UpdateAsync(DigitizationTask task);
    }
}
