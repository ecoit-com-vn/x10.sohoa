using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
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

        public WorkflowEngineService(IWorkflowRepository workflowRepository, IEnumerable<IWorkflowIntegrationHandler> handlers)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        private IWorkflowIntegrationHandler? GetHandler(string entityType)
        {
            return _handlers.FirstOrDefault(h => h.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<WorkflowInstance> SubmitAsync(Guid definitionId, string targetEntityId, string targetEntityType, string userId)
        {
            var definition = await _workflowRepository.GetDefinitionByIdAsync(definitionId);
            if (definition == null)
            {
                throw new KeyNotFoundException("Không tìm thấy định nghĩa quy trình BPMN.");
            }

            var steps = definition.Steps.OrderBy(s => s.Order).ToList();
            if (steps.Count == 0)
            {
                throw new InvalidOperationException("Quy trình chưa cấu hình bất kỳ bước duyệt nào.");
            }

            var activeInstance = await _workflowRepository.GetInstanceByEntityAsync(targetEntityId, targetEntityType);
            if (activeInstance != null && activeInstance.Status == "Running")
            {
                throw new InvalidOperationException("Hồ sơ/yêu cầu này đang trong một quy trình phê duyệt khác.");
            }

            string? currentNodeId = null;
            string? currentNodeName = null;

            if (!string.IsNullOrEmpty(definition.BpmnXml))
            {
                try
                {
                    XDocument xmlDoc = XDocument.Parse(definition.BpmnXml);
                    XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
                    var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
                    if (process != null)
                    {
                        var startEvent = process.Elements(bpmn + "startEvent").FirstOrDefault();
                        if (startEvent != null)
                        {
                            var startEventId = startEvent.Attribute("id")?.Value;
                            var outgoingFlow = process.Elements(bpmn + "sequenceFlow")
                                .FirstOrDefault(f => f.Attribute("sourceRef")?.Value == startEventId);
                            if (outgoingFlow != null)
                            {
                                var nextId = outgoingFlow.Attribute("targetRef")?.Value;
                                if (!string.IsNullOrEmpty(nextId))
                                {
                                    var nextNode = process.Elements().FirstOrDefault(e => e.Attribute("id")?.Value == nextId);
                                    if (nextNode != null)
                                    {
                                        if (nextNode.Name.LocalName.Contains("Gateway"))
                                        {
                                            var gwFlow = process.Elements(bpmn + "sequenceFlow")
                                                .FirstOrDefault(f => f.Attribute("sourceRef")?.Value == nextId);
                                            if (gwFlow != null)
                                            {
                                                var targetTaskId = gwFlow.Attribute("targetRef")?.Value;
                                                if (!string.IsNullOrEmpty(targetTaskId))
                                                {
                                                    var targetTask = process.Elements().FirstOrDefault(e => e.Attribute("id")?.Value == targetTaskId);
                                                    if (targetTask != null)
                                                    {
                                                        currentNodeId = targetTaskId;
                                                        currentNodeName = targetTask.Attribute("name")?.Value;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            currentNodeId = nextId;
                                            currentNodeName = nextNode.Attribute("name")?.Value;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(currentNodeName))
            {
                currentNodeName = steps[0].StepName;
            }

            var instance = new WorkflowInstance
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = definitionId,
                TargetEntityId = targetEntityId,
                TargetEntityType = targetEntityType,
                Status = "Running",
                CurrentStepOrder = steps[0].Order,
                CurrentNodeId = currentNodeId,
                CurrentNodeName = currentNodeName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var firstStep = steps[0];
            var task = new WorkflowTask
            {
                Id = Guid.NewGuid(),
                WorkflowInstanceId = instance.Id,
                StepId = firstStep.Id,
                StepName = firstStep.StepName,
                AssignedRole = firstStep.RequiredRole,
                AssigneeUserId = userId,
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
                Comment = $"Khởi tạo quy trình '{definition.Name}' cho đối tượng {targetEntityId}.",
                ActionDate = DateTime.UtcNow
            };

            await _workflowRepository.CreateInstanceAsync(instance);
            await _workflowRepository.CreateTaskAsync(task);
            await _workflowRepository.AddHistoryAsync(history);

            // Trigger integration handler started hook
            var handler = GetHandler(targetEntityType);
            if (handler != null)
            {
                await handler.OnWorkflowStartedAsync(targetEntityId, instance.Id);
                await handler.OnWorkflowStateChangedAsync(targetEntityId, instance.Id, instance.CurrentNodeName);
            }

            await _workflowRepository.SaveChangesAsync();
            return instance;
        }

        public async Task<WorkflowInstance> SubmitByEntityTypeAsync(string targetEntityId, string entityType, string targetEntityType, string userId)
        {
            var definition = await _workflowRepository.GetActiveDefinitionByEntityTypeAsync(entityType)
                ?? throw new KeyNotFoundException(
                    $"Không tìm thấy quy trình đang hoạt động cho '{entityType}'. " +
                    "Hãy tạo WorkflowDefinition với EntityType tương ứng và bật trạng thái Active.");

            // targetEntityType (vd: "Dossier") gắn vào instance để query sau,
            // entityType (vd: "Quy trình số hóa hồ sơ") chỉ dùng tìm definition.
            return await SubmitAsync(definition.Id, targetEntityId, targetEntityType, userId);
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

                nextTask = new WorkflowTask
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepId = nextStep.Id,
                    StepName = nextStep.StepName,
                    AssignedRole = nextStep.RequiredRole,
                    AssigneeUserId = nextAssigneeUserId,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                await _workflowRepository.CreateTaskAsync(nextTask);

                var handler = GetHandler(instance.TargetEntityType);
                if (handler != null)
                {
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, nextStep.StepName);
                }
            }
            else
            {
                instance.Status = "Completed";
                instance.CurrentNodeId = null;
                instance.CurrentNodeName = "Hoàn thành";
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);

                // Trigger integration handler completed hook
                var handler = GetHandler(instance.TargetEntityType);
                if (handler != null)
                {
                    await handler.OnWorkflowCompletedAsync(instance.TargetEntityId, instance.Id);
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, "Hoàn thành");
                }
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
            await _workflowRepository.AddHistoryAsync(history);

            await _workflowRepository.SaveChangesAsync();
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
            task.AssigneeUserId = userId;
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

                prevTask = new WorkflowTask
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepId = prevStep.Id,
                    StepName = prevStep.StepName,
                    AssignedRole = prevStep.RequiredRole,
                    AssigneeUserId = instance.Tasks
                        .Where(t => t.StepId == prevStep.Id)
                        .OrderByDescending(t => t.CreatedAt)
                        .FirstOrDefault()?.AssigneeUserId,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                await _workflowRepository.CreateTaskAsync(prevTask);

                var handler = GetHandler(instance.TargetEntityType);
                if (handler != null)
                {
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, prevStep.StepName);
                }
            }
            else
            {
                instance.Status = "Terminated";
                instance.CurrentNodeId = null;
                instance.CurrentNodeName = "Đã từ chối";
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);

                // Trigger integration handler rejected/terminated hook
                var handler = GetHandler(instance.TargetEntityType);
                if (handler != null)
                {
                    await handler.OnWorkflowRejectedAsync(instance.TargetEntityId, instance.Id);
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, "Đã từ chối");
                }
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
            await _workflowRepository.AddHistoryAsync(history);

            await _workflowRepository.SaveChangesAsync();
            return prevTask;
        }

        public async Task<WorkflowInstance> MoveAsync(string targetEntityId, string nextNodeId, string userId, string actionLabel, string? comment = null, string? nextAssigneeUserId = null, string entityType = "BorrowRecord")
        {
            var instance = await _workflowRepository.GetInstanceByEntityAsync(targetEntityId, entityType);
            if (instance == null)
            {
                throw new KeyNotFoundException("Không tìm thấy phiên chạy quy trình cho hồ sơ này.");
            }

            if (instance.Status != "Running")
            {
                throw new InvalidOperationException("Quy trình đã kết thúc hoặc bị hủy.");
            }

            var currentTask = instance.Tasks.FirstOrDefault(t => t.Status == "Pending");
            if (currentTask == null)
            {
                throw new InvalidOperationException("Không tìm thấy nhiệm vụ đang chờ phê duyệt.");
            }

            string action = (actionLabel.Equals("Từ chối", StringComparison.OrdinalIgnoreCase) || actionLabel.Equals("reject", StringComparison.OrdinalIgnoreCase)) ? "Reject" : "Approve";
            currentTask.Status = action == "Reject" ? "Returned" : "Completed";
            currentTask.CompletedAt = DateTime.UtcNow;
            currentTask.AssigneeUserId = userId;
            await _workflowRepository.UpdateTaskAsync(currentTask);

            var definition = instance.WorkflowDefinition;
            if (definition == null || string.IsNullOrEmpty(definition.BpmnXml))
            {
                throw new InvalidOperationException("Quy trình chưa cấu hình sơ đồ BPMN XML.");
            }

            XDocument xmlDoc = XDocument.Parse(definition.BpmnXml);
            XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
            var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
            if (process == null)
            {
                throw new InvalidOperationException("Thẻ process không tồn tại trong cấu hình XML.");
            }

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

            if (nextType == "endEvent")
            {
                instance.Status = action == "Reject" ? "Terminated" : "Completed";
                instance.CurrentNodeId = nextNodeId;
                instance.CurrentNodeName = action == "Reject" ? "Đã từ chối" : "Hoàn thành";
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);

                var endHistory = new WorkflowHistory
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepName = currentTask.StepName,
                    Action = action,
                    ActionByUserId = userId,
                    Comment = comment ?? (action == "Reject" ? "Từ chối và kết thúc quy trình." : "Phê duyệt bước cuối. Kết thúc quy trình."),
                    ActionDate = DateTime.UtcNow
                };
                await _workflowRepository.AddHistoryAsync(endHistory);

                var handler = GetHandler(instance.TargetEntityType);
                if (handler != null)
                {
                    if (action == "Reject")
                    {
                        await handler.OnWorkflowRejectedAsync(instance.TargetEntityId, instance.Id);
                    }
                    else
                    {
                        await handler.OnWorkflowCompletedAsync(instance.TargetEntityId, instance.Id);
                    }
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, instance.CurrentNodeName);
                }
            }
            else if (nextType == "task" || nextType == "userTask")
            {
                var nextStepName = nextNode.Attribute("name")?.Value ?? nextNodeId;
                var requiredRole = nextNode.Attribute("requiredRole")?.Value ?? string.Empty;

                var matchedStep = definition.Steps.FirstOrDefault(s => s.StepName.Equals(nextStepName, StringComparison.OrdinalIgnoreCase));
                var stepId = matchedStep?.Id ?? Guid.Empty;

                if (matchedStep != null)
                {
                    instance.CurrentStepOrder = matchedStep.Order;
                }
                instance.CurrentNodeId = nextNodeId;
                instance.CurrentNodeName = nextStepName;
                instance.UpdatedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateInstanceAsync(instance);

                string? assigneeUserId = null;
                if (action == "Reject")
                {
                    var lastTaskForStep = instance.Tasks
                        .Where(t => t.StepId == stepId || t.StepName.Equals(nextStepName, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(t => t.CreatedAt)
                        .FirstOrDefault();
                    assigneeUserId = lastTaskForStep?.AssigneeUserId;
                }
                else
                {
                    assigneeUserId = nextAssigneeUserId;
                }

                var nextTask = new WorkflowTask
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepId = stepId,
                    StepName = nextStepName,
                    AssignedRole = requiredRole,
                    AssigneeUserId = assigneeUserId,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                await _workflowRepository.CreateTaskAsync(nextTask);

                var moveHistory = new WorkflowHistory
                {
                    Id = Guid.NewGuid(),
                    WorkflowInstanceId = instance.Id,
                    StepName = currentTask.StepName,
                    Action = action,
                    ActionByUserId = userId,
                    Comment = comment ?? (action == "Reject" ? $"Yêu cầu làm lại, trả về bước '{nextStepName}'." : $"Chuyển tiếp bước duyệt đến '{nextStepName}'."),
                    ActionDate = DateTime.UtcNow
                };
                await _workflowRepository.AddHistoryAsync(moveHistory);

                var handler = GetHandler(instance.TargetEntityType);
                if (handler != null)
                {
                    await handler.OnWorkflowStateChangedAsync(instance.TargetEntityId, instance.Id, nextStepName);
                }
            }
            else
            {
                throw new InvalidOperationException($"Loại phần tử tiếp theo '{nextType}' không được hỗ trợ trong việc chuyển bước.");
            }

            await _workflowRepository.SaveChangesAsync();
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
            string entityType = "BorrowRecord")
        {
            var instance = await _workflowRepository.GetInstanceByEntityAsync(targetEntityId, entityType);
            if (instance == null)
            {
                throw new KeyNotFoundException("Không tìm thấy phiên chạy quy trình cho hồ sơ này.");
            }

            if (instance.Status != "Running")
            {
                throw new InvalidOperationException("Quy trình đã kết thúc hoặc bị hủy.");
            }

            var currentTask = instance.Tasks.FirstOrDefault(t => t.Status == "Pending");
            if (currentTask == null)
            {
                throw new InvalidOperationException("Không tìm thấy nhiệm vụ đang chờ phê duyệt.");
            }

            // 1. Role Check
            if (!isAdmin)
            {
                if (!string.IsNullOrEmpty(currentTask.AssignedRole))
                {
                    var hasRole = userRoles.Any(r => r.Equals(currentTask.AssignedRole, StringComparison.OrdinalIgnoreCase));
                    if (!hasRole)
                    {
                        throw new ArgumentException($"Người dùng không có vai trò '{currentTask.AssignedRole}' cần thiết cho bước này.");
                    }
                }
            }

            var definition = instance.WorkflowDefinition;
            if (definition == null || string.IsNullOrEmpty(definition.BpmnXml))
            {
                throw new InvalidOperationException("Quy trình chưa cấu hình sơ đồ BPMN XML.");
            }

            // 2. BPMN XML Path Validation
            XDocument xmlDoc = XDocument.Parse(definition.BpmnXml);
            var sourceNodeId = instance.CurrentNodeId ?? string.Empty;
            
            var isFallback = nextNodeId.Equals("approve", StringComparison.OrdinalIgnoreCase) || 
                             nextNodeId.Equals("reject", StringComparison.OrdinalIgnoreCase);
                             
            if (!isFallback && !string.IsNullOrEmpty(sourceNodeId))
            {
                var isValidPath = IsValidBpmnPath(xmlDoc, sourceNodeId, nextNodeId);
                if (!isValidPath)
                {
                    throw new ArgumentException($"Chuyển bước không hợp lệ: Không có đường đi từ node hiện tại '{sourceNodeId}' đến '{nextNodeId}' trong sơ đồ BPMN.");
                }
            }

            // 3. Perform movement via MoveAsync
            return await MoveAsync(targetEntityId, nextNodeId, userId, actionLabel, comment, nextAssigneeUserId, entityType);
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
                    var entityType = task.WorkflowInstance.TargetEntityType;
                    var entityId = task.WorkflowInstance.TargetEntityId;
                    if (!entityDetailsLookup.TryGetValue((entityType, entityId), out targetDetails!))
                        targetDetails = $"{entityType}: {entityId}";
                }

                result.Add(new
                {
                    TaskId = task.Id,
                    InstanceId = task.WorkflowInstanceId,
                    DefinitionId = task.WorkflowInstance?.WorkflowDefinitionId,
                    DefinitionName = task.WorkflowInstance?.WorkflowDefinition?.Name ?? "",
                    TargetEntityId = task.WorkflowInstance?.TargetEntityId ?? "",
                    TargetEntityType = task.WorkflowInstance?.TargetEntityType ?? "",
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

        private async Task<IReadOnlyDictionary<(string EntityType, string EntityId), string>> BuildEntityDetailsLookupAsync(
            IReadOnlyList<WorkflowTask> tasks,
            bool includeEntityDetails)
        {
            var lookup = new Dictionary<(string EntityType, string EntityId), string>();
            if (!includeEntityDetails || tasks.Count == 0) return lookup;

            var grouped = tasks
                .Where(t => t.WorkflowInstance != null)
                .GroupBy(t => t.WorkflowInstance!.TargetEntityType, StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                var entityType = group.Key;
                var entityIds = group
                    .Select(t => t.WorkflowInstance!.TargetEntityId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var handler = GetHandler(entityType);
                if (handler == null)
                {
                    foreach (var entityId in entityIds)
                        lookup[(entityType, entityId)] = $"{entityType}: {entityId}";
                    continue;
                }

                var batchDetails = await handler.GetEntityDetailsBatchAsync(entityIds);
                foreach (var entityId in entityIds)
                {
                    lookup[(entityType, entityId)] = batchDetails.TryGetValue(entityId, out var detail)
                        ? detail
                        : $"{entityType}: {entityId}";
                }
            }

            return lookup;
        }

        public async Task<IEnumerable<WorkflowHistory>> GetHistoryAsync(Guid instanceId)
        {
            return await _workflowRepository.GetHistoryByInstanceIdAsync(instanceId);
        }

        public async Task<IEnumerable<WorkflowHistory>> GetHistoryByEntityAsync(string entityId, string entityType)
        {
            var instance = await _workflowRepository.GetInstanceByEntityAsync(entityId, entityType);
            if (instance == null) return Enumerable.Empty<WorkflowHistory>();
            return await _workflowRepository.GetHistoryByInstanceIdAsync(instance.Id);
        }

        public async Task<object> GetInstanceStatusByEntityAsync(string entityId, string entityType)
        {
            var instance = await _workflowRepository.GetInstanceByEntityAsync(entityId, entityType);
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
                    ActionType = t.Step?.ActionType ?? "",
                    AllowEdit = t.Step?.AllowEdit ?? false,
                    t.CreatedAt
                })
            };
        }
    }
}
