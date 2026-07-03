using EvnHanoi.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Infrastructure.Services
{
    public class BorrowRecordService : IBorrowRecordService
    {
        private readonly IBorrowRecordRepository _borrowRepository;
        private readonly IWorkflowDefinitionService _workflowDefinitionService;
        private readonly IWorkflowEngineService _workflowEngine;

        public BorrowRecordService(
            IBorrowRecordRepository borrowRepository,
            IWorkflowDefinitionService workflowDefinitionService,
            IWorkflowEngineService workflowEngine)
        {
            _borrowRepository = borrowRepository ?? throw new ArgumentNullException(nameof(borrowRepository));
            _workflowDefinitionService = workflowDefinitionService ?? throw new ArgumentNullException(nameof(workflowDefinitionService));
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        }

        public async Task<IEnumerable<BorrowRecord>> GetAllAsync()
        {
            return await _borrowRepository.GetAllAsync();
        }

        public async Task<BorrowRecord?> GetByIdAsync(Guid id)
        {
            return await _borrowRepository.GetByIdAsync(id);
        }

        public async Task<BorrowRecord> CreateAsync(BorrowRecord record, string userId)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            record.Id = Guid.NewGuid();
            record.RequestDate = DateTime.UtcNow;
            record.State = BorrowState.Requested;

            var success = await _borrowRepository.CreateAsync(record);
            if (!success)
                throw new InvalidOperationException("Không thể lưu yêu cầu mượn/trả hồ sơ.");

            try
            {
                await _workflowEngine.SubmitByWorkflowTypeIdAsync(
                    record.Id.ToString(),
                    EntityType.BorrowRecord.Id,
                    userId);
            }
            catch (KeyNotFoundException)
            {
                record.State = BorrowState.Approved;
                record.ApprovedDate = DateTime.UtcNow;

                var note = " (Tự động duyệt - Chưa cấu hình quy trình phê duyệt)";
                record.Reason = string.IsNullOrEmpty(record.Reason) ? note : record.Reason + note;

                if (!await _borrowRepository.UpdateAsync(record))
                    throw new InvalidOperationException("Không thể cập nhật yêu cầu mượn/trả hồ sơ.");
            }

            return record;
        }

        public async Task<bool> UpdateStateAsync(Guid id, BorrowState newState)
        {
            var record = await _borrowRepository.GetByIdAsync(id);
            if (record == null) return false;

            // Simple State Machine validation
            if (record.State == BorrowState.Requested && newState == BorrowState.Approved)
            {
                record.State = BorrowState.Approved;
                record.ApprovedDate = DateTime.UtcNow;
            }
            else if (record.State == BorrowState.Approved && newState == BorrowState.Borrowed)
            {
                record.State = BorrowState.Borrowed;
                record.BorrowedDate = DateTime.UtcNow;
            }
            else if (record.State == BorrowState.Borrowed && newState == BorrowState.Returned)
            {
                record.State = BorrowState.Approved;
                record.ReturnedDate = DateTime.UtcNow;
            }
            else
            {
                throw new InvalidOperationException("Chuyển trạng thái yêu cầu không hợp lệ.");
            }

            return await _borrowRepository.UpdateAsync(record);
        }

        public async Task<(IEnumerable<BorrowRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, BorrowState? state = null)
        {
            return await _borrowRepository.GetPagedAsync(page, pageSize, keyword, state);
        }

        public async Task<WorkflowInstance> MoveWorkflowAsync(string dossierId, string nextNodeId, string userId, string actionLabel, string? comment, string? nextAssigneeUserId = null)
        {
            return await _workflowEngine.MoveAsync(dossierId, nextNodeId, userId, actionLabel, comment, nextAssigneeUserId);
        }

        public async Task<WorkflowInstance> MoveWorkflowWithValidationAsync(Guid id, string dossierId, string nextNodeId, string userId, List<string> userRoles, bool isAdmin, string actionLabel, string? comment, string? nextAssigneeUserId = null)
        {
            return await _workflowEngine.MoveWithValidationAsync(id.ToString(), nextNodeId, userId, userRoles, isAdmin, actionLabel, comment, nextAssigneeUserId);
        }

        public async Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId)
        {
            return await _workflowEngine.GetMyTasksAsync(userRoles, isAdmin, userId);
        }

        public async Task<IEnumerable<WorkflowHistory>> GetWorkflowHistoryAsync(Guid borrowRecordId)
        {
            var record = await _borrowRepository.GetByIdAsync(borrowRecordId);
            if (record == null || !record.WorkflowInstanceId.HasValue)
            {
                return Enumerable.Empty<WorkflowHistory>();
            }
            return await _workflowEngine.GetHistoryAsync(record.WorkflowInstanceId.Value);
        }

        public async Task<object> GetWorkflowStatusByEntityAsync(string entityId, int workflowTypeId)
        {
            return await _workflowEngine.GetInstanceStatusByEntityAsync(entityId, workflowTypeId);
        }

        public async Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid definitionId)
        {
            return await _workflowDefinitionService.GetDefinitionByIdAsync(definitionId);
        }
    }
}
