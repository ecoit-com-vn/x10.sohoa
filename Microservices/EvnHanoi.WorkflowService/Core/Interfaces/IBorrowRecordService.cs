using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IBorrowRecordService
    {
        Task<IEnumerable<BorrowRecord>> GetAllAsync();
        Task<BorrowRecord?> GetByIdAsync(Guid id);
        Task<BorrowRecord> CreateAsync(BorrowRecord record, string userId);
        Task<bool> UpdateStateAsync(Guid id, BorrowState newState);
        Task<WorkflowInstance> MoveWorkflowAsync(string dossierId, string nextNodeId, string userId, string actionLabel, string? comment);
        Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin);
        Task<IEnumerable<WorkflowHistory>> GetWorkflowHistoryAsync(Guid borrowRecordId);
        Task<object> GetWorkflowStatusByEntityAsync(string entityId, string entityType);
        Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid definitionId);
    }
}
