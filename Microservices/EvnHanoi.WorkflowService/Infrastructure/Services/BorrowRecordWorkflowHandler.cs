using EvnHanoi.Infrastructure.Enums;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Infrastructure.Services
{
    public class BorrowRecordWorkflowHandler : IWorkflowIntegrationHandler
    {
        private readonly IBorrowRecordRepository _borrowRepository;
        private readonly IWorkflowRepository _workflowRepository;

        public int WorkflowTypeId => EntityType.BorrowRecord.Id;

        public BorrowRecordWorkflowHandler(IBorrowRecordRepository borrowRepository, IWorkflowRepository workflowRepository)
        {
            _borrowRepository = borrowRepository ?? throw new ArgumentNullException(nameof(borrowRepository));
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
        }

        public Task OnWorkflowStartedAsync(string entityId, Guid instanceId)
        {
            // No action needed on start, but can log if necessary
            return Task.CompletedTask;
        }

        public async Task OnWorkflowCompletedAsync(string entityId, Guid instanceId)
        {
            if (Guid.TryParse(entityId, out Guid brId))
            {
                var br = await _borrowRepository.GetByIdAsync(brId);
                if (br != null)
                {
                    br.State = BorrowState.Approved;
                    br.ApprovedDate = DateTime.UtcNow;
                    await _borrowRepository.UpdateAsync(br);
                }
            }
        }

        public async Task OnWorkflowRejectedAsync(string entityId, Guid instanceId)
        {
            if (Guid.TryParse(entityId, out Guid brId))
            {
                var br = await _borrowRepository.GetByIdAsync(brId);
                if (br != null)
                {
                    br.State = BorrowState.Returned; // Set to Returned on rejection
                    await _borrowRepository.UpdateAsync(br);
                }
            }
        }

        public async Task OnWorkflowStateChangedAsync(string entityId, Guid instanceId, string statusName)
        {
            if (Guid.TryParse(entityId, out Guid brId))
            {
                var br = await _borrowRepository.GetByIdAsync(brId);
                if (br != null)
                {
                    br.WorkflowInstanceId = instanceId;
                    br.WorkflowStatusName = statusName;

                    if (statusName.Equals("Cán bộ tạo đơn mượn hồ sơ", StringComparison.OrdinalIgnoreCase))
                    {
                        var history = await _workflowRepository.GetHistoryByInstanceIdAsync(instanceId);
                        var lastAction = history.OrderByDescending(h => h.ActionDate).FirstOrDefault();
                        if (lastAction != null && lastAction.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
                        {
                            br.State = BorrowState.Returned;
                        }
                    }
                    else if (br.State == BorrowState.Returned)
                    {
                        br.State = BorrowState.Requested;
                    }

                    await _borrowRepository.UpdateAsync(br);
                }
            }
        }

        public async Task<string> GetEntityDetailsAsync(string entityId)
        {
            if (Guid.TryParse(entityId, out Guid brId))
            {
                var br = await _borrowRepository.GetByIdAsync(brId);
                if (br != null)
                {
                    return $"Mượn hồ sơ: {br.DossierId} - Lý do: {br.Reason}";
                }
            }
            return "Yêu cầu mượn/trả hồ sơ";
        }

        public async Task<IReadOnlyDictionary<string, string>> GetEntityDetailsBatchAsync(IReadOnlyCollection<string> entityIds)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (entityIds.Count == 0) return result;

            var guidIds = entityIds
                .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var records = guidIds.Count > 0
                ? (await _borrowRepository.GetSummaryByIdsAsync(guidIds)).ToList()
                : new List<BorrowRecord>();

            var byId = records.ToDictionary(r => r.Id.ToString(), StringComparer.OrdinalIgnoreCase);
            foreach (var entityId in entityIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (byId.TryGetValue(entityId, out var br))
                    result[entityId] = $"Mượn hồ sơ: {br.DossierId} - Lý do: {br.Reason}";
                else
                    result[entityId] = "Yêu cầu mượn/trả hồ sơ";
            }

            return result;
        }
    }
}
