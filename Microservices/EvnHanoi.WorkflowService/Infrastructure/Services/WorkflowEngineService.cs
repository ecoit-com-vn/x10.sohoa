using EvnHanoi.Infrastructure.Enums;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EvnHanoi.WorkflowService.Infrastructure.Services
{
    public class WorkflowEngineService : IWorkflowEngineService
    {
        private readonly IWorkflowRepository _workflowRepository;
        private readonly IEnumerable<IWorkflowIntegrationHandler> _handlers;
        private readonly WorkflowDefinitionCacheService _definitionCache;
        private readonly IMemoryCache _memoryCache;

        public WorkflowEngineService(
            IWorkflowRepository workflowRepository,
            IEnumerable<IWorkflowIntegrationHandler> handlers,
            WorkflowDefinitionCacheService definitionCache,
            IMemoryCache memoryCache)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            _definitionCache = definitionCache ?? throw new ArgumentNullException(nameof(definitionCache));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        private IWorkflowIntegrationHandler? GetHandler(int workflowTypeId) =>
            _handlers.FirstOrDefault(h => h.WorkflowTypeId == workflowTypeId);

        public Task<WorkflowInstance> SubmitByWorkflowTypeIdAsync(string entityId, int workflowTypeId, string userId)
        {
            var item = EntityType.RequireById(workflowTypeId);

            return SubmitInternalAsync(
                async () =>
                {
                    var definition = await _definitionCache.GetActiveDefinitionByWorkflowTypeIdAsync(item.Id);
                    if (definition == null)
                        throw new KeyNotFoundException(
                            $"Không tìm thấy quy trình đang hoạt động cho loại quy trình '{item.Name}'. " +
                            "Hãy tạo WorkflowDefinition tương ứng và bật trạng thái Active.");
                    return definition;
                },
                entityId,
                item.Id,
                userId);
        }

        private async Task<WorkflowInstance> SubmitInternalAsync(
            Func<Task<WorkflowDefinition>> loadDefinition,
            string entityId,
            int workflowTypeId,
            string userId)
        {
            var definition = await loadDefinition();

            var steps = definition.Steps.OrderBy(s => s.Order).ToList();
            if (steps.Count == 0)
                throw new InvalidOperationException("Quy trình chưa cấu hình bất kỳ bước duyệt nào.");

            if (await _workflowRepository.ExistsRunningInstanceAsync(entityId, workflowTypeId))
                throw new InvalidOperationException("Hồ sơ/yêu cầu này đang trong một quy trình phê duyệt khác.");

            var firstNode = BpmnFirstNodeResolver.Resolve(definition, _memoryCache);
            var currentNodeId = firstNode.NodeId;
            var firstStep = steps[0];
            var currentNodeName = WorkflowDisplayNameHelper.Resolve(
                firstStep.StepName,
                firstNode.NodeName,
                currentNodeId);

            var instance = new WorkflowInstance
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = definition.Id,
                TargetEntityId = entityId,
                WorkflowTypeId = workflowTypeId,
                Status = "Running",
                CurrentStepOrder = steps[0].Order,
                CurrentNodeId = currentNodeId,
                CurrentNodeName = currentNodeName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var task = new WorkflowTask
            {
                Id = Guid.NewGuid(),
                WorkflowInstanceId = instance.Id,
                StepId = firstStep.Id,
                StepName = firstStep.StepName,
                AssignedRole = firstStep.RequiredRole,
                AssigneeUserId = ShouldAssignSubmitterAsFirstStepAssignee(firstStep) ? userId : null,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            var history = new WorkflowHistory
            {
                Id = Guid.NewGuid(),
                WorkflowInstanceId = instance.Id,
                StepName = "Bắt đầu quy trình",
                Action = "Submit",
                ActionByUserId = userId,
                Comment = $"Khởi tạo quy trình '{definition.Name}' cho đối tượng {entityId}.",
                ActionDate = DateTime.UtcNow
            };

            await _workflowRepository.CreateSubmitBatchAsync(instance, task, history);

            var handler = GetHandler(workflowTypeId);
            if (handler != null)
                await handler.OnWorkflowStartedAsync(entityId, instance.Id);

            return instance;
        }


        public async Task<WorkflowTask> ApproveAsync(Guid taskId, string userId, string? comment = null, string? nextAssigneeUserId = null)
        {
            var task = await _workflowRepository.GetTaskByIdAsync(taskId);
            if (task == null)
            {
                throw new KeyNotFoundException("Không tìm thấy nhiệm vụ phê duyệt.");
            }

            if (task.Status != "Pending")
            {
                throw new InvalidOperationException("Nhiệm vụ này đã được xử lý trước đó.");
            }

            task.Status = "Completed";
            task.CompletedAt = DateTime.UtcNow;
            task.AssigneeUserId = userId;
            await _workflowRepository.UpdateTaskAsync(task);

            var instance = task.WorkflowInstance;
            if (instance == null)
            {
                throw new InvalidOperationException("Nhiệm vụ không gắn với phiên quy trình hợp lệ.");
            }

            var steps = instance.WorkflowDefinition?.Steps.OrderBy(s => s.Order).ToList() ?? new List<WorkflowStep>();
            var currentStepIndex = steps.FindIndex(s => s.Id == task.StepId);

            WorkflowTask? nextTask = null;

            if (currentStepIndex != -1 && currentStepIndex < steps.Count - 1)
            {
                var nextStep = steps[currentStepIndex + 1];
                instance.CurrentStepOrder = nextStep.Order;

                string? nextNodeId = null;
                if (!string.IsNullOrEmpty(instance.WorkflowDefinition?.BpmnXml))
                {
                    try
                    {
                        XDocument xmlDoc = XDocument.Parse(instance.WorkflowDefinition.BpmnXml);
                        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
                        var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
                        if (process != null)
                        {
                            var taskNode = process.Elements()
                                .FirstOrDefault(e => (e.Name.LocalName == "task" || e.Name.LocalName == "userTask") && 
                                                      e.Attribute("name")?.Value.Equals(nextStep.StepName, StringComparison.OrdinalIgnoreCase) == true);
                            if (taskNode != null)
                            {
                                nextNodeId = taskNode.Attribute("id")?.Value;
                            }
                        }
                    }
                    catch { }
                }

                instance.CurrentNodeId = nextNodeId;
                instance.CurrentNodeName = nextStep.StepName;
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);

                if (string.IsNullOrWhiteSpace(nextAssigneeUserId))
                    throw new ArgumentException("Vui lòng chọn người xử lý bước tiếp theo.");

                nextTask = new WorkflowTask
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepId = nextStep.Id,
                    StepName = nextStep.StepName,
                    AssignedRole = nextStep.RequiredRole,
                    AssigneeUserId = nextAssigneeUserId.Trim(),
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                if (!await _workflowRepository.CreateTaskAsync(nextTask))
                    throw new InvalidOperationException("Không thể tạo nhiệm vụ bước tiếp theo.");
            }
            else
            {
                instance.Status = "Completed";
                instance.CurrentNodeId = null;
                instance.CurrentNodeName = "Hoàn thành";
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);
            }

            var history = new WorkflowHistory
            {
                Id = Guid.NewGuid(),
                WorkflowInstanceId = instance.Id,
                StepName = task.StepName,
                Action = "Approve",
                ActionByUserId = userId,
                Comment = comment ?? "Đã phê duyệt.",
                ActionDate = DateTime.UtcNow
            };
            if (!await _workflowRepository.AddHistoryAsync(history))
                throw new InvalidOperationException("Không thể ghi lịch sử phê duyệt.");

            var handler = GetHandler(instance.WorkflowTypeId);
            if (handler != null)
            {
                if (nextTask != null)
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, nextTask.StepName);
                else
                {
                    await handler.OnWorkflowCompletedAsync(instance.TargetEntityId, instance.Id);
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, "Hoàn thành");
                }
            }

            return task;
        }

        public async Task<WorkflowTask?> RejectAsync(Guid taskId, string userId, string? comment = null)
        {
            var task = await _workflowRepository.GetTaskByIdAsync(taskId);
            if (task == null)
            {
                throw new KeyNotFoundException("Không tìm thấy nhiệm vụ phê duyệt.");
            }

            if (task.Status != "Pending")
            {
                throw new InvalidOperationException("Nhiệm vụ này đã được xử lý trước đó.");
            }

            task.Status = "Returned";
            task.CompletedAt = DateTime.UtcNow;
            // Giữ AssigneeUserId inbox — actor nằm ở history ActionByUserId.
            await _workflowRepository.UpdateTaskAsync(task);

            var instance = task.WorkflowInstance;
            if (instance == null)
            {
                throw new InvalidOperationException("Nhiệm vụ không gắn với phiên quy trình hợp lệ.");
            }

            var steps = instance.WorkflowDefinition?.Steps.OrderBy(s => s.Order).ToList() ?? new List<WorkflowStep>();
            var currentStepIndex = steps.FindIndex(s => s.Id == task.StepId);

            WorkflowTask? prevTask = null;

            if (currentStepIndex > 0)
            {
                var prevStep = steps[currentStepIndex - 1];
                instance.CurrentStepOrder = prevStep.Order;

                string? prevNodeId = null;
                if (!string.IsNullOrEmpty(instance.WorkflowDefinition?.BpmnXml))
                {
                    try
                    {
                        XDocument xmlDoc = XDocument.Parse(instance.WorkflowDefinition.BpmnXml);
                        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
                        var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
                        if (process != null)
                        {
                            var taskNode = process.Elements()
                                .FirstOrDefault(e => (e.Name.LocalName == "task" || e.Name.LocalName == "userTask") && 
                                                      e.Attribute("name")?.Value.Equals(prevStep.StepName, StringComparison.OrdinalIgnoreCase) == true);
                            if (taskNode != null)
                            {
                                prevNodeId = taskNode.Attribute("id")?.Value;
                            }
                        }
                    }
                    catch { }
                }

                instance.CurrentNodeId = prevNodeId;
                instance.CurrentNodeName = prevStep.StepName;
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);

                var returnAssigneeUserId = await ResolveReturnStepAssigneeAsync(
                    instance.Id,
                    instance.Tasks,
                    prevStep,
                    prevStep.StepName);

                prevTask = new WorkflowTask
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepId = prevStep.Id,
                    StepName = prevStep.StepName,
                    AssignedRole = prevStep.RequiredRole,
                    AssigneeUserId = returnAssigneeUserId,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                if (!await _workflowRepository.CreateTaskAsync(prevTask))
                    throw new InvalidOperationException("Không thể tạo nhiệm vụ trả về bước trước.");
            }
            else
            {
                instance.Status = "Terminated";
                instance.CurrentNodeId = null;
                instance.CurrentNodeName = "Đã từ chối";
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);
            }

            var history = new WorkflowHistory
            {
                Id = Guid.NewGuid(),
                WorkflowInstanceId = instance.Id,
                StepName = task.StepName,
                Action = "Reject",
                ActionByUserId = userId,
                Comment = comment ?? "Bị từ chối / trả lại.",
                ActionDate = DateTime.UtcNow
            };
            if (!await _workflowRepository.AddHistoryAsync(history))
                throw new InvalidOperationException("Không thể ghi lịch sử từ chối.");

            var handler = GetHandler(instance.WorkflowTypeId);
            if (handler != null)
            {
                if (currentStepIndex > 0)
                {
                    var prevStep = steps[currentStepIndex - 1];
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, prevStep.StepName);
                }
                else
                {
                    await handler.OnWorkflowRejectedAsync(instance.TargetEntityId, instance.Id);
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, "Đã từ chối");
                }
            }

            return prevTask;
        }

        public async Task<WorkflowInstance> MoveAsync(string targetEntityId, string nextNodeId, string userId, string actionLabel, string? comment = null, string? nextAssigneeUserId = null, int workflowTypeId = 2)
        {
            var instance = await GetRunningMoveContextAsync(targetEntityId, workflowTypeId);
            return await MoveCoreAsync(instance, nextNodeId, userId, actionLabel, comment, nextAssigneeUserId, preParsedXml: null);
        }

        private async Task<WorkflowInstance> GetRunningMoveContextAsync(string targetEntityId, int workflowTypeId)
        {
            var instance = await _workflowRepository.GetInstanceByEntityAsync(targetEntityId, workflowTypeId);
            if (instance == null)
                throw new KeyNotFoundException("Không tìm thấy phiên chạy quy trình cho hồ sơ này.");

            if (instance.Status != "Running")
                throw new InvalidOperationException("Quy trình đã kết thúc hoặc bị hủy.");

            if (instance.Tasks.All(t => t.Status != "Pending"))
                throw new InvalidOperationException("Không tìm thấy nhiệm vụ đang chờ phê duyệt.");

            return instance;
        }

        private async Task<WorkflowInstance> MoveCoreAsync(
            WorkflowInstance instance,
            string nextNodeId,
            string userId,
            string actionLabel,
            string? comment,
            string? nextAssigneeUserId,
            XDocument? preParsedXml)
        {
            var currentTask = instance.Tasks.First(t => t.Status == "Pending");

            var definition = instance.WorkflowDefinition
                ?? throw new InvalidOperationException("Quy trình chưa cấu hình sơ đồ BPMN XML.");
            if (string.IsNullOrEmpty(definition.BpmnXml))
                throw new InvalidOperationException("Quy trình chưa cấu hình sơ đồ BPMN XML.");

            var xmlDoc = preParsedXml ?? XDocument.Parse(definition.BpmnXml);
            XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
            var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault()
                ?? throw new InvalidOperationException("Thẻ process không tồn tại trong cấu hình XML.");

            var nextNode = process.Elements().FirstOrDefault(e => e.Attribute("id")?.Value == nextNodeId);
            if (nextNode == null)
            {
                if (nextNodeId.Equals("approve", StringComparison.OrdinalIgnoreCase))
                {
                    currentTask.Status = "Pending";
                    currentTask.CompletedAt = null;
                    currentTask.AssigneeUserId = null;
                    await _workflowRepository.UpdateTaskAsync(currentTask);

                    var completedTask = await ApproveAsync(currentTask.Id, userId, comment, nextAssigneeUserId);
                    return completedTask.WorkflowInstance ?? instance;
                }
                if (nextNodeId.Equals("reject", StringComparison.OrdinalIgnoreCase))
                {
                    currentTask.Status = "Pending";
                    currentTask.CompletedAt = null;
                    currentTask.AssigneeUserId = null;
                    await _workflowRepository.UpdateTaskAsync(currentTask);

                    var prevTask = await RejectAsync(currentTask.Id, userId, comment);
                    return prevTask?.WorkflowInstance ?? instance;
                }

                throw new KeyNotFoundException($"Không tìm thấy phần tử tiếp theo với ID '{nextNodeId}' trong sơ đồ quy trình.");
            }

            var nextType = nextNode.Name.LocalName;
            var isReject = IsRejectAction(actionLabel);
            var action = isReject ? "Reject" : "Approve";

            currentTask.Status = isReject ? "Returned" : "Completed";
            currentTask.CompletedAt = DateTime.UtcNow;
            // Reject: giữ assignee inbox cũ (THU_KHO…) — người từ chối ghi trong history.
            if (!isReject)
                currentTask.AssigneeUserId = userId;

            if (nextType == "endEvent")
            {
                instance.Status = isReject ? "Terminated" : "Completed";
                instance.CurrentNodeId = nextNodeId;
                instance.CurrentNodeName = isReject ? "Đã từ chối" : "Hoàn thành";
                instance.UpdatedAt = DateTime.UtcNow;

                var endHistory = new WorkflowHistory
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepName = currentTask.StepName,
                    Action = action,
                    ActionByUserId = userId,
                    Comment = comment ?? (isReject ? "Từ chối và kết thúc quy trình." : "Phê duyệt bước cuối. Kết thúc quy trình."),
                    ActionDate = DateTime.UtcNow
                };

                await _workflowRepository.ExecuteMoveBatchAsync(currentTask, instance, null, endHistory);

                var handler = GetHandler(instance.WorkflowTypeId);
                if (handler != null)
                {
                    if (isReject)
                        await handler.OnWorkflowRejectedAsync(instance.TargetEntityId, instance.Id);
                    else
                        await handler.OnWorkflowCompletedAsync(instance.TargetEntityId, instance.Id);
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, instance.CurrentNodeName);
                }
            }
            else if (nextType == "task" || nextType == "userTask")
            {
                var nextStepName = nextNode.Attribute("name")?.Value ?? nextNodeId;
                var targetStep = FindStepByName(definition, nextStepName);
                var stepId = targetStep?.Id ?? Guid.Empty;
                var requiredRole = !string.IsNullOrWhiteSpace(targetStep?.RequiredRole)
                    ? targetStep!.RequiredRole
                    : (nextNode.Attribute("requiredRole")?.Value ?? string.Empty);

                if (targetStep != null)
                    instance.CurrentStepOrder = targetStep.Order;

                // Instance luôn Running khi chuyển sang bước task (Pending chỉ ở WORKFLOWTASKS).
                instance.Status = "Running";
                instance.CurrentNodeId = nextNodeId;
                instance.CurrentNodeName = nextStepName;
                instance.UpdatedAt = DateTime.UtcNow;

                string? assigneeUserId;
                if (isReject)
                {
                    assigneeUserId = await ResolveReturnStepAssigneeAsync(
                        instance.Id, instance.Tasks, targetStep, nextStepName);
                }
                else
                {
                    // Giao việc đích danh chỉ là gợi ý mặc định (FE đã chọn sẵn) — người chuyển bước
                    // vẫn có quyền chọn người khác, nên ưu tiên nextAssigneeUserId do FE gửi lên trước;
                    // chỉ dùng targetStep.AssigneeId khi caller không gửi kèm người xử lý (vd. các luồng
                    // cũ không có picker chọn người).
                    if (!string.IsNullOrWhiteSpace(nextAssigneeUserId))
                    {
                        assigneeUserId = nextAssigneeUserId.Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(targetStep?.AssigneeId))
                    {
                        assigneeUserId = targetStep!.AssigneeId.Trim();
                    }
                    else
                    {
                        throw new ArgumentException("Vui lòng chọn người xử lý bước tiếp theo.");
                    }
                }

                var nextTask = new WorkflowTask
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepId = stepId,
                    StepName = targetStep?.StepName ?? nextStepName,
                    AssignedRole = requiredRole,
                    AssigneeUserId = assigneeUserId,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                var moveHistory = new WorkflowHistory
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepName = currentTask.StepName,
                    Action = action,
                    ActionByUserId = userId,
                    Comment = comment ?? (isReject
                        ? $"Yêu cầu làm lại, trả về bước '{nextStepName}'."
                        : $"Chuyển tiếp bước duyệt đến '{nextStepName}'."),
                    ActionDate = DateTime.UtcNow
                };

                await _workflowRepository.ExecuteMoveBatchAsync(currentTask, instance, nextTask, moveHistory);
                instance.Tasks.Add(nextTask);

                var handler = GetHandler(instance.WorkflowTypeId);
                if (handler != null)
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, nextStepName);
            }
            else
            {
                throw new InvalidOperationException($"Loại phần tử tiếp theo '{nextType}' không được hỗ trợ trong việc chuyển bước.");
            }

            return instance;
        }

        public async Task<WorkflowInstance> MoveWithValidationAsync(
            string targetEntityId, 
            string nextNodeId, 
            string userId, 
            List<string> userRoles, 
            bool isAdmin, 
            string actionLabel, 
            string? comment = null,
            string? nextAssigneeUserId = null,
            int workflowTypeId = 2)
        {
            var instance = await GetRunningMoveContextAsync(targetEntityId, workflowTypeId);
            var currentTask = instance.Tasks.First(t => t.Status == "Pending");

            if (!isAdmin)
            {
                if (string.IsNullOrEmpty(currentTask.AssigneeUserId))
                    throw new ArgumentException("Nhiệm vụ chưa được gán cho người xử lý cụ thể.");

                if (!currentTask.AssigneeUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Nhiệm vụ này đã được chỉ định cho người xử lý khác.");
            }

            var definition = instance.WorkflowDefinition;
            if (definition == null || string.IsNullOrEmpty(definition.BpmnXml))
                throw new InvalidOperationException("Quy trình chưa cấu hình sơ đồ BPMN XML.");

            var xmlDoc = XDocument.Parse(definition.BpmnXml);
            var sourceNodeId = instance.CurrentNodeId ?? string.Empty;

            var isFallback = nextNodeId.Equals("approve", StringComparison.OrdinalIgnoreCase) ||
                             nextNodeId.Equals("reject", StringComparison.OrdinalIgnoreCase);

            if (!isFallback && !string.IsNullOrEmpty(sourceNodeId))
            {
                if (!IsValidBpmnPath(xmlDoc, sourceNodeId, nextNodeId))
                {
                    throw new ArgumentException($"Chuyển bước không hợp lệ: Không có đường đi từ node hiện tại '{sourceNodeId}' đến '{nextNodeId}' trong sơ đồ BPMN.");
                }
            }

            return await MoveCoreAsync(instance, nextNodeId, userId, actionLabel, comment, nextAssigneeUserId, xmlDoc);
        }

        private bool IsValidBpmnPath(XDocument xmlDoc, string sourceNodeId, string targetNodeId)
        {
            if (string.IsNullOrEmpty(sourceNodeId) || string.IsNullOrEmpty(targetNodeId))
                return false;

            if (sourceNodeId == targetNodeId)
                return true;

            XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
            var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
            if (process == null) return false;

            var flows = process.Elements(bpmn + "sequenceFlow")
                .Select(f => new { 
                    Source = f.Attribute("sourceRef")?.Value, 
                    Target = f.Attribute("targetRef")?.Value 
                })
                .Where(f => !string.IsNullOrEmpty(f.Source) && !string.IsNullOrEmpty(f.Target))
                .GroupBy(f => f.Source!)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Target!).ToList());

            var elementTypes = process.Elements()
                .Where(e => e.Attribute("id")?.Value != null)
                .ToDictionary(e => e.Attribute("id")!.Value, e => e.Name.LocalName);

            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(sourceNodeId);
            visited.Add(sourceNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == targetNodeId)
                    return true;

                if (flows.TryGetValue(current, out var nextNodes))
                {
                    foreach (var next in nextNodes)
                     {
                         if (!visited.Contains(next))
                         {
                             var isTarget = next == targetNodeId;
                             elementTypes.TryGetValue(next, out var type);
                             var isGateway = type != null && type.Contains("Gateway", StringComparison.OrdinalIgnoreCase);

                             if (isTarget || isGateway)
                             {
                                 visited.Add(next);
                                 queue.Enqueue(next);
                             }
                         }
                     }
                }
            }

            return false;
        }

        public async Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId, Guid? workflowInstanceId = null)
        {
            var tasks = (await _workflowRepository.GetPendingTasksByRolesAsync(userRoles, isAdmin, userId, workflowInstanceId)).ToList();
            var includeEntityDetails = !workflowInstanceId.HasValue;
            var entityDetailsLookup = await BuildEntityDetailsLookupAsync(tasks, includeEntityDetails);
            var result = new List<object>();

            foreach (var task in tasks)
            {
                string targetDetails = "";
                if (includeEntityDetails && task.WorkflowInstance != null)
                {
                    var workflowTypeId = task.WorkflowInstance.WorkflowTypeId;
                    var entityId = task.WorkflowInstance.TargetEntityId;
                    if (!entityDetailsLookup.TryGetValue((workflowTypeId, entityId), out targetDetails!))
                        targetDetails = $"Loại quy trình {workflowTypeId}: {entityId}";
                }

                result.Add(new
                {
                    TaskId = task.Id,
                    InstanceId = task.WorkflowInstanceId,
                    DefinitionId = task.WorkflowInstance?.WorkflowDefinitionId,
                    DefinitionName = task.WorkflowInstance?.WorkflowDefinition?.Name ?? "",
                    TargetEntityId = task.WorkflowInstance?.TargetEntityId ?? "",
                    WorkflowTypeId = task.WorkflowInstance?.WorkflowTypeId ?? 0,
                    TargetDetails = targetDetails,
                    StepId = task.StepId,
                    StepName = task.StepName,
                    AssignedRole = task.AssignedRole,
                    ActionType = task.Step?.ActionType ?? "",
                    Status = task.Status,
                    CreatedAt = task.CreatedAt
                });
            }

            return result;
        }

        private async Task<IReadOnlyDictionary<(int WorkflowTypeId, string EntityId), string>> BuildEntityDetailsLookupAsync(
            IReadOnlyList<WorkflowTask> tasks,
            bool includeEntityDetails)
        {
            var lookup = new Dictionary<(int WorkflowTypeId, string EntityId), string>();
            if (!includeEntityDetails || tasks.Count == 0) return lookup;

            var grouped = tasks
                .Where(t => t.WorkflowInstance != null)
                .GroupBy(t => t.WorkflowInstance!.WorkflowTypeId);

            foreach (var group in grouped)
            {
                var workflowTypeId = group.Key;
                var entityIds = group
                    .Select(t => t.WorkflowInstance!.TargetEntityId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var handler = GetHandler(workflowTypeId);
                if (handler == null)
                {
                    foreach (var entityId in entityIds)
                        lookup[(workflowTypeId, entityId)] = $"Loại quy trình {workflowTypeId}: {entityId}";
                    continue;
                }

                var batchDetails = await handler.GetEntityDetailsBatchAsync(entityIds);
                foreach (var entityId in entityIds)
                {
                    lookup[(workflowTypeId, entityId)] = batchDetails.TryGetValue(entityId, out var detail)
                        ? detail
                        : $"Loại quy trình {workflowTypeId}: {entityId}";
                }
            }

            return lookup;
        }

        public async Task<IEnumerable<WorkflowHistory>> GetHistoryAsync(Guid instanceId)
        {
            return await _workflowRepository.GetHistoryByInstanceIdAsync(instanceId);
        }

        public async Task<IEnumerable<WorkflowHistory>> GetHistoryByEntityAsync(string entityId, int workflowTypeId)
        {
            var instance = await _workflowRepository.GetInstanceByEntityAsync(entityId, workflowTypeId, false);
            if (instance == null) return Enumerable.Empty<WorkflowHistory>();
            return await _workflowRepository.GetHistoryByInstanceIdAsync(instance.Id);
        }

        public async Task<object> GetInstanceStatusByEntityAsync(string entityId, int workflowTypeId)
        {
            var instance = await _workflowRepository.GetInstanceByEntityAsync(entityId, workflowTypeId, false);
            if (instance == null)
            {
                throw new KeyNotFoundException("Không tìm thấy phiên chạy quy trình cho hồ sơ/yêu cầu này.");
            }

            var pendingTask = instance.Tasks.FirstOrDefault(t => t.Status == "Pending");
            var currentStep = pendingTask?.Step
                ?? instance.WorkflowDefinition?.Steps.FirstOrDefault(s => s.Order == instance.CurrentStepOrder);
            var currentStepAllowEdit = instance.Status == "Running" && (currentStep?.AllowEdit ?? false);

            return new
            {
                InstanceId = instance.Id,
                WorkflowDefinitionId = instance.WorkflowDefinitionId,
                CurrentNodeId = instance.CurrentNodeId,
                DefinitionName = instance.WorkflowDefinition?.Name ?? "",
                Status = instance.Status,
                CurrentStepOrder = instance.CurrentStepOrder,
                CurrentStepName = instance.CurrentNodeName ?? currentStep?.StepName ?? "",
                CurrentStepAllowEdit = currentStepAllowEdit,
                CreatedAt = instance.CreatedAt,
                UpdatedAt = instance.UpdatedAt,
                PendingTasks = instance.Tasks.Where(t => t.Status == "Pending").Select(t => new {
                    t.Id,
                    t.StepName,
                    t.AssignedRole,
                    t.AssigneeUserId,
                    ActionType = t.Step?.ActionType ?? "",
                    AllowEdit = t.Step?.AllowEdit ?? false,
                    t.CreatedAt
                })
            };
        }

        /// <summary>Khớp FE isRejectLabel.</summary>
        private static bool IsRejectAction(string? actionLabel)
        {
            if (string.IsNullOrWhiteSpace(actionLabel))
                return false;

            var l = actionLabel.Trim().ToLowerInvariant();
            return l.Contains("từ chối")
                || l.Contains("hủy")
                || l.Contains("reject")
                || l.Contains("cancel")
                || l.Contains("trả lại");
        }

        private static WorkflowStep? FindStepByName(WorkflowDefinition definition, string stepName) =>
            definition.Steps.FirstOrDefault(s =>
                s.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Chỉ dùng khi trả lại (reject): suy assignee bước trước từ task/history — không thay chọn người trên FE.
        /// </summary>
        private async Task<string?> ResolveReturnStepAssigneeAsync(
            Guid instanceId,
            IEnumerable<WorkflowTask> tasks,
            WorkflowStep? targetStep,
            string stepName)
        {
            var stepId = targetStep?.Id ?? Guid.Empty;
            var fromPriorTask = tasks
                .Where(t => (stepId != Guid.Empty && t.StepId == stepId)
                    || t.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase))
                .Where(t => !t.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(t.AssigneeUserId))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => t.AssigneeUserId)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromPriorTask))
                return fromPriorTask;

            var fromDb = await _workflowRepository.GetPriorStepAssigneeAsync(instanceId, stepId, stepName);
            if (!string.IsNullOrWhiteSpace(fromDb))
                return fromDb;

            var history = await _workflowRepository.GetHistoryByInstanceIdAsync(instanceId);
            return history
                .Where(h => h.Action.Equals("Submit", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(h.ActionByUserId))
                .OrderBy(h => h.ActionDate)
                .Select(h => h.ActionByUserId)
                .FirstOrDefault();
        }

        /// <summary>
        /// Bước đầu AllowEdit → gán người submit; bước role pool → chỉ AssignedRole (ES inbox theo role).
        /// </summary>
        private static bool ShouldAssignSubmitterAsFirstStepAssignee(WorkflowStep step)
        {
            if (step.AllowEdit)
                return true;

            // Tự gán người submit nếu bước không yêu cầu bất kỳ nhóm quyền hoặc vai trò nào
            return string.IsNullOrWhiteSpace(step.RequiredRole)
                && string.IsNullOrWhiteSpace(step.SystemPermissionGroupIds)
                && string.IsNullOrWhiteSpace(step.UnitPermissionGroupIds);
        }
    }
}
