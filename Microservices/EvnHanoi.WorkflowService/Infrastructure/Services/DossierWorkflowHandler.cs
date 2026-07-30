using EvnHanoi.Infrastructure.Enums;
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
    /// Tích hợp quy trình cho hồ sơ (WORKFLOW_TYPE_ID = Dossier).
    /// WorkflowService SỞ HỮU logic suy ra DossierStatus từ sự kiện quy trình,
    /// rồi đồng bộ ngược về EquipmentService qua API nội bộ (KHÔNG expose ra Gateway).
    /// </summary>
    public class DossierWorkflowHandler : IWorkflowIntegrationHandler
    {
        // Khớp các hằng DossierStatus của EquipmentService.
        private const int StatusPendingApproval = 3;
        private const int StatusInProgress = 4;
        private const int StatusReturned = 5;
        private const int StatusApproved = 6;

        private readonly IWorkflowRepository _workflowRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMessageProducer _messageProducer;
        private readonly ILogger<DossierWorkflowHandler> _logger;

        public virtual int WorkflowTypeId => EntityType.Dossier.Id;

        public DossierWorkflowHandler(
            IWorkflowRepository workflowRepository,
            IHttpClientFactory httpClientFactory,
            IMessageProducer messageProducer,
            ILogger<DossierWorkflowHandler> logger)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _messageProducer = messageProducer ?? throw new ArgumentNullException(nameof(messageProducer));
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
        private async Task SyncAsync(string entityId, Guid instanceId, int? dossierStatusOverride = null)
        {
            if (!Guid.TryParse(entityId, out _)) return;

            var instance = await _workflowRepository.GetInstanceByIdAsync(instanceId);
            if (instance == null)
            {
                _logger.LogWarning("DossierWorkflowHandler: không tìm thấy instance {InstanceId} khi đồng bộ hồ sơ {EntityId}.", instanceId, entityId);
                return;
            }

            var dossierStatus = dossierStatusOverride
                ?? await DeriveDossierStatusAsync(instance);

            var workflowStatusName = await ResolveWorkflowStatusNameAsync(instance);

            // Bóc tách assignees hiện tại từ các Tasks đang Pending
            var currentAssignees = new List<string>();
            if (instance.Tasks != null)
            {
                var pendingTasks = instance.Tasks.Where(t => t.Status == "Pending").ToList();
                foreach (var task in pendingTasks)
                {
                    if (!string.IsNullOrEmpty(task.AssigneeUserId))
                    {
                        currentAssignees.Add(task.AssigneeUserId);
                    }
                }
            }
            currentAssignees = currentAssignees.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var isRunning = string.Equals(instance.Status, "Running", StringComparison.OrdinalIgnoreCase);

            if (isRunning && currentAssignees.Count > 0)
            {
                try
                {
                    await _messageProducer.PublishToExchangeAsync(
                        new EvnHanoi.Infrastructure.Messaging.DossierMovedEvent
                        {
                            DossierId = entityId,
                            InstanceId = instanceId,
                            StepName = instance.CurrentNodeName,
                            RecipientUserIds = currentAssignees,
                            Timestamp = DateTime.UtcNow
                        },
                        EvnHanoi.Infrastructure.Messaging.NotificationTopicTopology.ExchangeName,
                        EvnHanoi.Infrastructure.Messaging.NotificationTopicTopology.DossierMovedRoutingKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DossierWorkflowHandler: không thể phát sự kiện thông báo cho hồ sơ {EntityId}.", entityId);
                }
            }

            // Bóc tách các action khả dụng từ BPMN XML (chỉ khi WF còn chạy)
            var availableActions = new List<WorkflowActionDto>();
            if (isRunning && !string.IsNullOrEmpty(instance.CurrentNodeId))
            {
                availableActions = GetAvailableActionsFromBpmn(instance.WorkflowDefinition?.BpmnXml, instance.CurrentNodeId);
            }

            var payload = new UpdateInternalWorkflowStateRequest
            {
                WorkflowInstanceId = instanceId,
                WorkflowStatusName = workflowStatusName,
                DossierStatusId = dossierStatus,
                CurrentStepId = isRunning ? instance.CurrentNodeId : null,
                CurrentAssignees = isRunning ? currentAssignees : new(),
                AvailableActions = isRunning ? availableActions : new()
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

        private List<WorkflowActionDto> GetAvailableActionsFromBpmn(string? bpmnXml, string currentNodeId)
        {
            var list = new List<WorkflowActionDto>();
            if (string.IsNullOrEmpty(bpmnXml) || string.IsNullOrEmpty(currentNodeId))
                return list;

            try
            {
                var xmlDoc = System.Xml.Linq.XDocument.Parse(bpmnXml);
                System.Xml.Linq.XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
                var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
                if (process == null) return list;

                var startEvent = process.Elements(bpmn + "startEvent").FirstOrDefault();
                var startEventId = startEvent?.Attribute("id")?.Value;
                var isFirstStep = !string.IsNullOrEmpty(startEventId) &&
                                  process.Elements(bpmn + "sequenceFlow").Any(f => f.Attribute("sourceRef")?.Value == startEventId && f.Attribute("targetRef")?.Value == currentNodeId);

                var flows = process.Elements(bpmn + "sequenceFlow")
                    .Where(f => f.Attribute("sourceRef")?.Value == currentNodeId)
                    .ToList();

                foreach (var flow in flows)
                {
                    var targetRef = flow.Attribute("targetRef")?.Value;
                    var name = flow.Attribute("name")?.Value;

                    if (string.IsNullOrEmpty(targetRef)) continue;

                    var targetNode = process.Descendants()
                        .FirstOrDefault(e => e.Attribute("id")?.Value == targetRef);
                    var targetType = targetNode?.Name?.LocalName ?? string.Empty;

                    if (targetType.Contains("Gateway", StringComparison.OrdinalIgnoreCase))
                    {
                        var gwFlows = process.Elements(bpmn + "sequenceFlow")
                            .Where(f => f.Attribute("sourceRef")?.Value == targetRef)
                            .ToList();

                        foreach (var gwFlow in gwFlows)
                        {
                            var gwTargetRef = gwFlow.Attribute("targetRef")?.Value;
                            var gwName = gwFlow.Attribute("name")?.Value;

                            if (string.IsNullOrEmpty(gwTargetRef)) continue;

                            var actionName = ResolveActionName(gwName, isFirstStep);
                            list.Add(BuildWorkflowAction(process, actionName, gwTargetRef));
                        }
                    }
                    else
                    {
                        var actionName = ResolveActionName(name, isFirstStep);
                        list.Add(BuildWorkflowAction(process, actionName, targetRef));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DossierWorkflowHandler: Lỗi parse BPMN XML để lấy Available Actions.");
            }

            return list;
        }

        private static string ResolveActionName(string? flowName, bool isFirstStep)
        {
            var actionName = !string.IsNullOrEmpty(flowName) ? flowName : (isFirstStep ? "Gửi duyệt" : "Chuyển tiếp");
            if (isFirstStep && (actionName.Equals("Chuyển tiếp", StringComparison.OrdinalIgnoreCase)
                || actionName.Equals("Tiếp tục", StringComparison.OrdinalIgnoreCase)))
            {
                actionName = "Gửi duyệt";
            }
            return actionName;
        }

        private static WorkflowActionDto BuildWorkflowAction(
            System.Xml.Linq.XElement process,
            string actionName,
            string nextNodeId)
        {
            var isReject = IsRejectActionName(actionName);
            var requiresNext = !isReject && IsUserTaskNode(process, nextNodeId);
            var nextRole = isReject ? null : GetNodeRequiredRole(process, nextNodeId);
            var assigneeConfig = isReject
                ? default
                : GetNodeAssigneeConfig(process, nextNodeId);

            return new WorkflowActionDto
            {
                Code = isReject ? "REJECT" : "APPROVE",
                Name = actionName,
                NextNodeId = nextNodeId,
                RequiresNextAssignee = requiresNext,
                NextStepRole = string.IsNullOrWhiteSpace(nextRole) ? null : nextRole.Trim(),
                UnitGroupIds = string.IsNullOrWhiteSpace(assigneeConfig.unitGroupIds) ? null : assigneeConfig.unitGroupIds,
                SystemGroupIds = string.IsNullOrWhiteSpace(assigneeConfig.systemGroupIds) ? null : assigneeConfig.systemGroupIds,
                RequireSameUnit = assigneeConfig.requireSameUnit,
                StaticAssigneeId = string.IsNullOrWhiteSpace(assigneeConfig.staticAssigneeId) ? null : assigneeConfig.staticAssigneeId
            };
        }

        private static bool IsRejectActionName(string actionName)
        {
            return actionName.Contains("từ chối", StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("trả lại", StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("reject", StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("hủy", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUserTaskNode(System.Xml.Linq.XElement process, string nodeId)
        {
            var node = process.Descendants()
                .FirstOrDefault(e => e.Attribute("id")?.Value == nodeId);
            if (node == null) return false;
            var local = node.Name.LocalName;
            return local.Equals("task", StringComparison.OrdinalIgnoreCase)
                || local.Equals("userTask", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetNodeRequiredRole(System.Xml.Linq.XElement process, string nodeId)
        {
            var node = process.Descendants()
                .FirstOrDefault(e => e.Attribute("id")?.Value == nodeId);
            return node?.Attribute("requiredRole")?.Value;
        }

        /// <summary>
        /// Đọc cấu hình nhóm quyền hệ thống/đơn vị, "chỉ cùng đơn vị" và "Người cụ thể" từ attribute
        /// BPMN của bước đích — khớp đúng tên attribute mà workflow-builder ghi ra (xem
        /// dossier-workflow-bpmn.util.ts:getAssigneeConfig phía frontend).
        /// </summary>
        private static (string? unitGroupIds, string? systemGroupIds, bool requireSameUnit, string? staticAssigneeId)
            GetNodeAssigneeConfig(System.Xml.Linq.XElement process, string nodeId)
        {
            var node = process.Descendants()
                .FirstOrDefault(e => e.Attribute("id")?.Value == nodeId);
            if (node == null) return (null, null, false, null);

            return (
                node.Attribute("unitPermissionGroupIds")?.Value,
                node.Attribute("systemPermissionGroupIds")?.Value,
                node.Attribute("requireSameUnit")?.Value == "true",
                node.Attribute("assigneeId")?.Value
            );
        }

        private async Task<int> DeriveDossierStatusAsync(Models.WorkflowInstance instance)
        {
            if (string.Equals(instance.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return StatusApproved;

            if (string.Equals(instance.Status, "Terminated", StringComparison.OrdinalIgnoreCase))
                return StatusReturned;

            var lastAction = await _workflowRepository.GetLastHistoryActionAsync(instance.Id);
            if (string.IsNullOrWhiteSpace(lastAction) || lastAction.Equals("Submit", StringComparison.OrdinalIgnoreCase))
                return StatusPendingApproval;

            if (string.Equals(lastAction, "Reject", StringComparison.OrdinalIgnoreCase))
                return IsAtCreatorFirstStep(instance) ? StatusReturned : StatusInProgress;

            return StatusInProgress;
        }

        /// <summary>
        /// Reject quay về bước đầu (người tạo) → Returned; reject về bước giữa → InProgress.
        /// </summary>
        private static bool IsAtCreatorFirstStep(Models.WorkflowInstance instance)
        {
            var steps = instance.WorkflowDefinition?.Steps;
            if (steps is null || steps.Count == 0)
                return instance.CurrentStepOrder <= 1;

            var firstOrder = steps.Min(s => s.Order);
            return instance.CurrentStepOrder == firstOrder;
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
            public int DossierStatusId { get; set; }
            public string? CurrentStepId { get; set; }
            public List<string> CurrentAssignees { get; set; } = new();
            public List<WorkflowActionDto> AvailableActions { get; set; } = new();
        }

        public class WorkflowActionDto
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NextNodeId { get; set; } = string.Empty;
            public bool RequiresNextAssignee { get; set; }
            public string? NextStepRole { get; set; }
            public string? UnitGroupIds { get; set; }
            public string? SystemGroupIds { get; set; }
            public bool RequireSameUnit { get; set; }
            public string? StaticAssigneeId { get; set; }
        }
    }
}
