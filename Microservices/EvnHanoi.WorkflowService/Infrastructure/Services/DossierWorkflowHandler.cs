using EvnHanoi.WorkflowService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Infrastructure.Services
{
    /// <summary>
    /// Tích hợp quy trình cho hồ sơ (EntityType = "Dossier").
    /// WorkflowService SỞ HỮU logic suy ra DossierStatus từ sự kiện quy trình,
    /// rồi đồng bộ ngược về EquipmentService qua API nội bộ (KHÔNG expose ra Gateway).
    /// </summary>
    public class DossierWorkflowHandler : IWorkflowIntegrationHandler
    {
        // Khớp các hằng DossierStatus của EquipmentService.
        private const string StatusPendingApproval = "PendingApproval";
        private const string StatusInProgress = "InProgress";
        private const string StatusReturned = "Returned";
        private const string StatusApproved = "Approved";

        private readonly IWorkflowRepository _workflowRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DossierWorkflowHandler> _logger;

        public string EntityType => "Dossier";

        public DossierWorkflowHandler(
            IWorkflowRepository workflowRepository,
            IHttpClientFactory httpClientFactory,
            ILogger<DossierWorkflowHandler> logger)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task OnWorkflowStartedAsync(string entityId, Guid instanceId) =>
            SyncAsync(entityId, instanceId, StatusPendingApproval);

        public Task OnWorkflowCompletedAsync(string entityId, Guid instanceId) => SyncAsync(entityId, instanceId);

        public Task OnWorkflowRejectedAsync(string entityId, Guid instanceId) => SyncAsync(entityId, instanceId);

        public Task OnWorkflowStateChangedAsync(string entityId, Guid instanceId, string statusName) =>
            SyncAsync(entityId, instanceId);

        /// <summary>
        /// Đọc instance + history hiện tại, suy ra DossierStatus và đẩy về EquipmentService.
        /// </summary>
        private async Task SyncAsync(string entityId, Guid instanceId, string? dossierStatusOverride = null)
        {
            if (!Guid.TryParse(entityId, out _)) return;

            var instance = await _workflowRepository.GetInstanceByIdAsync(instanceId);
            if (instance == null)
            {
                _logger.LogWarning("DossierWorkflowHandler: không tìm thấy instance {InstanceId} khi đồng bộ hồ sơ {EntityId}.", instanceId, entityId);
                return;
            }

            var dossierStatus = dossierStatusOverride
                ?? await DeriveDossierStatusAsync(instance.Status, instanceId);

            var workflowStatusName = await ResolveWorkflowStatusNameAsync(instance);

            var payload = new UpdateInternalWorkflowStateRequest
            {
                WorkflowInstanceId = instanceId,
                WorkflowStatusName = workflowStatusName,
                DossierStatus = dossierStatus
            };

            try
            {
                var client = _httpClientFactory.CreateClient("EquipmentService");
                var response = await client.PutAsJsonAsync($"internal/v1/dossiers/{entityId}/workflow-state", payload);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "DossierWorkflowHandler: đồng bộ trạng thái hồ sơ {EntityId} thất bại ({StatusCode}): {Body}",
                        entityId, (int)response.StatusCode, body);
                    throw new InvalidOperationException(
                        $"Không thể đồng bộ trạng thái hồ sơ sau quy trình ({(int)response.StatusCode}).");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DossierWorkflowHandler: lỗi gọi EquipmentService đồng bộ trạng thái hồ sơ {EntityId}.", entityId);
                throw new InvalidOperationException(
                    "Không thể kết nối EquipmentService để đồng bộ trạng thái hồ sơ sau quy trình.", ex);
            }
        }

        private async Task<string> DeriveDossierStatusAsync(string instanceStatus, Guid instanceId)
        {
            if (string.Equals(instanceStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                return StatusApproved;

            if (string.Equals(instanceStatus, "Terminated", StringComparison.OrdinalIgnoreCase))
                return StatusReturned;

            // WF còn chạy: Reject chỉ là trả về bước trước — không coi là tab Trả lại.
            var lastAction = await _workflowRepository.GetLastHistoryActionAsync(instanceId);
            if (string.IsNullOrWhiteSpace(lastAction) || lastAction.Equals("Submit", StringComparison.OrdinalIgnoreCase))
                return StatusPendingApproval;

            return StatusInProgress;
        }

        private async Task<string?> ResolveWorkflowStatusNameAsync(Models.WorkflowInstance instance)
        {
            var pendingStepName = await _workflowRepository.GetPendingStepNameAsync(instance.Id);
            return WorkflowDisplayNameHelper.Resolve(
                pendingStepName,
                instance.CurrentNodeName,
                instance.CurrentNodeId);
        }

        public async Task<string> GetEntityDetailsAsync(string entityId)
        {
            await Task.CompletedTask;
            return $"Hồ sơ số hóa: {entityId}";
        }

        public async Task<IReadOnlyDictionary<string, string>> GetEntityDetailsBatchAsync(IReadOnlyCollection<string> entityIds)
        {
            await Task.CompletedTask;
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in entityIds.Distinct(StringComparer.OrdinalIgnoreCase))
                result[id] = $"Hồ sơ số hóa: {id}";
            return result;
        }

        private class UpdateInternalWorkflowStateRequest
        {
            public Guid WorkflowInstanceId { get; set; }
            public string? WorkflowStatusName { get; set; }
            public string DossierStatus { get; set; } = string.Empty;
        }
    }
}
