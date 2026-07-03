using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Core.Interfaces
{
    public interface IBorrowRecordService
    {
        Task<IEnumerable<BorrowRecord>> GetAllAsync();
        Task<(IEnumerable<BorrowRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, BorrowState? state = null);
        Task<BorrowRecord?> GetByIdAsync(Guid id);
        Task<BorrowRecord> CreateAsync(BorrowRecord record, string userId);
        Task<bool> UpdateStateAsync(Guid id, BorrowState newState);
        Task<WorkflowInstance> MoveWorkflowAsync(string dossierId, string nextNodeId, string userId, string actionLabel, string? comment, string? nextAssigneeUserId = null);
        Task<WorkflowInstance> MoveWorkflowWithValidationAsync(Guid id, string dossierId, string nextNodeId, string userId, List<string> userRoles, bool isAdmin, string actionLabel, string? comment, string? nextAssigneeUserId = null);
        Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId);
        Task<IEnumerable<WorkflowHistory>> GetWorkflowHistoryAsync(Guid borrowRecordId);
        Task<object> GetWorkflowStatusByEntityAsync(string entityId, int workflowTypeId);
        Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid definitionId);
    }
}
