using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Controllers;

/// <summary>
/// Logic chung workflow hồ sơ — subclass gắn WorkflowTypeId và route riêng.
/// Luôn lấy definition active + version mới nhất qua GetActiveDefinitionByWorkflowTypeIdAsync.
/// </summary>
[Authorize]
[ApiController]
public abstract class DossierWorkflowControllerBase : ControllerBase
{
    private readonly IWorkflowEngineService _workflowEngine;
    private readonly IWorkflowDefinitionService _workflowDefinitionService;
    private readonly IDossierWorkflowQueryService _dossierWorkflowQuery;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IConfiguration _configuration;

    protected abstract int WorkflowTypeId { get; }
    protected abstract bool AutoApproveWhenNoDefinition { get; }

    protected DossierWorkflowControllerBase(
        IWorkflowEngineService workflowEngine,
        IWorkflowDefinitionService workflowDefinitionService,
        IDossierWorkflowQueryService dossierWorkflowQuery,
        IHttpClientFactory httpClientFactory,
        IWorkflowRepository workflowRepository,
        IConfiguration configuration)
    {
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _workflowDefinitionService = workflowDefinitionService ?? throw new ArgumentNullException(nameof(workflowDefinitionService));
        _dossierWorkflowQuery = dossierWorkflowQuery ?? throw new ArgumentNullException(nameof(dossierWorkflowQuery));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "system";

    protected List<string> UserRoles => User.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList();

    protected bool IsAdmin => User.IsInRole("ADMIN") || UserRoles.Contains("ADMIN");

    [HttpGet("next-step-info")]
    public async Task<IActionResult> GetNextStepInfo()
    {
        try
        {
            var definition = await _workflowRepository.GetActiveDefinitionByWorkflowTypeIdAsync(WorkflowTypeId);
            if (definition == null)
            {
                if (AutoApproveWhenNoDefinition)
                {
                    return Ok(new
                    {
                        autoApprove = true,
                        message = "Chưa cấu hình quy trình — hồ sơ sẽ được tự động phê duyệt khi gửi."
                    });
                }

                return NotFound(new { message = "Không tìm thấy quy trình đang hoạt động cho hồ sơ." });
            }

            var steps = definition.Steps.OrderBy(s => s.Order).ToList();
            if (steps.Count < 2)
            {
                return BadRequest(new { message = "Quy trình phê duyệt phải cấu hình ít nhất 2 bước." });
            }

            var step2 = steps[1];
            string nextNodeId = string.Empty;
            if (!string.IsNullOrEmpty(definition.BpmnXml))
            {
                try
                {
                    var xmlDoc = System.Xml.Linq.XDocument.Parse(definition.BpmnXml);
                    System.Xml.Linq.XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
                    var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
                    if (process != null)
                    {
                        var startEvent = process.Element(bpmn + "startEvent");
                        var startEventId = startEvent?.Attribute("id")?.Value;
                        if (!string.IsNullOrEmpty(startEventId))
                        {
                            var flowFromStart = process.Elements(bpmn + "sequenceFlow")
                                .FirstOrDefault(f => f.Attribute("sourceRef")?.Value == startEventId);
                            var step1NodeId = flowFromStart?.Attribute("targetRef")?.Value;
                            if (!string.IsNullOrEmpty(step1NodeId))
                            {
                                var flowFromStep1 = process.Elements(bpmn + "sequenceFlow")
                                    .FirstOrDefault(f => f.Attribute("sourceRef")?.Value == step1NodeId);
                                nextNodeId = flowFromStep1?.Attribute("targetRef")?.Value ?? string.Empty;
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            if (string.IsNullOrEmpty(nextNodeId))
                nextNodeId = step2.Id.ToString();

            return Ok(new
            {
                autoApprove = false,
                nextNodeId,
                stepName = step2.StepName,
                requiredRole = step2.RequiredRole,
                // Hiện ô chọn người xử lý khi bước có bất kỳ cấu hình nào liên quan đến việc
                // xác định người xử lý (kể cả giao việc đích danh — khi đó FE hiện đúng 1 người,
                // đã khoá sẵn, để người dùng biết hồ sơ sẽ được giao cho ai).
                requiresNextAssignee = !string.IsNullOrEmpty(step2.RequiredRole)
                                       || !string.IsNullOrEmpty(step2.SystemPermissionGroupIds)
                                       || !string.IsNullOrEmpty(step2.UnitPermissionGroupIds)
                                       || !string.IsNullOrEmpty(step2.AssigneeId),
                // Cờ và dữ liệu bổ sung cho Frontend lọc danh sách người xử lý
                requireSameUnit = step2.RequireSameUnit,
                systemGroupIds = step2.SystemPermissionGroupIds,
                unitGroupIds = step2.UnitPermissionGroupIds,
                staticAssigneeId = step2.AssigneeId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi lấy thông tin bước phê duyệt.", detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitAndMoveRequest request)
    {
        if (request == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var dossierDetail = await GetDossierDetailAsync(id);
        if (dossierDetail == null)
            return BadRequest(new { message = "Không thể kết nối dịch vụ thiết bị để kiểm tra trạng thái hồ sơ." });

        if (dossierDetail.StatusId != 2)
            return BadRequest(new { message = $"Hồ sơ phải ở trạng thái 'Hoàn thành' mới được phép gửi duyệt. Trạng thái hiện tại: {dossierDetail.Status}" });

        var activeDefinition = await _workflowRepository.GetActiveDefinitionByWorkflowTypeIdAsync(WorkflowTypeId);
        if (activeDefinition == null)
        {
            if (AutoApproveWhenNoDefinition)
            {
                try
                {
                    await AutoApproveDossierAsync(id);
                    return Ok(new
                    {
                        success = true,
                        autoApproved = true,
                        message = "Tự động phê duyệt — chưa cấu hình quy trình.",
                        data = new { dossierStatus = "Approved", workflowStepName = "Tự động duyệt" }
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "Không thể tự động phê duyệt hồ sơ.", detail = ex.Message });
                }
            }

            return NotFound(new { message = "Không tìm thấy quy trình đang hoạt động cho hồ sơ." });
        }

        WorkflowInstance? instance = null;
        try
        {
            instance = await _workflowEngine.SubmitByWorkflowTypeIdAsync(id.ToString(), WorkflowTypeId, UserId);
        }
        catch (KeyNotFoundException ex)
        {
            if (AutoApproveWhenNoDefinition)
            {
                try
                {
                    await AutoApproveDossierAsync(id);
                    return Ok(new
                    {
                        success = true,
                        autoApproved = true,
                        message = "Tự động phê duyệt — chưa cấu hình quy trình.",
                        data = new { dossierStatus = "Approved", workflowStepName = "Tự động duyệt" }
                    });
                }
                catch (Exception autoEx)
                {
                    return StatusCode(500, new { message = "Không thể tự động phê duyệt hồ sơ.", detail = autoEx.Message });
                }
            }

            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Khởi tạo quy trình phê duyệt thất bại.", detail = ex.Message });
        }

        try
        {
            // Dùng MoveAsync (bỏ qua kiểm tra "nhiệm vụ đã được gán cho đúng người") vì đây là
            // bước di chuyển ngay sau khi CHÍNH request này vừa tạo task bước 1 — không có khả năng
            // người khác đã nhận nhiệm vụ đó, nên việc kiểm tra AssigneeUserId ở đây chỉ gây lỗi giả
            // khi bước 1 có cấu hình RequiredRole/nhóm quyền (khi đó SubmitByWorkflowTypeIdAsync không
            // tự gán người nộp làm assignee bước 1, dẫn tới false positive "chưa được gán người xử lý").
            var updatedInstance = await _workflowEngine.MoveAsync(
                id.ToString(),
                request.NextNodeId,
                UserId,
                request.ActionLabel,
                request.Comment,
                request.NextAssigneeUserId,
                WorkflowTypeId);

            return Ok(new
            {
                success = true,
                message = "Gửi duyệt hồ sơ thành công.",
                data = new
                {
                    instanceId = updatedInstance.Id,
                    dossierStatus = "PendingApproval",
                    workflowStepName = updatedInstance.CurrentNodeName
                }
            });
        }
        catch (Exception ex)
        {
            if (instance != null)
            {
                try { await _workflowRepository.DeleteInstancePhysicalAsync(instance.Id); } catch { }

                try
                {
                    var client = CreateEquipmentClient();
                    var rollbackDto = new
                    {
                        dossierStatusId = 2,
                        workflowInstanceId = (Guid?)null,
                        workflowStepName = (string?)null
                    };
                    await client.PutAsJsonAsync($"internal/v1/dossiers/{id}/workflow-state", rollbackDto);
                }
                catch { }
            }

            if (ex is KeyNotFoundException knf) return NotFound(new { message = knf.Message });
            if (ex is ArgumentException arg) return BadRequest(new { message = arg.Message });
            if (ex is InvalidOperationException inv) return BadRequest(new { message = inv.Message });

            return StatusCode(500, new { message = "Lỗi khi chuyển tiếp hồ sơ đến người duyệt tiếp theo.", detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resubmit")]
    public async Task<IActionResult> Resubmit(Guid id, [FromBody] MoveDossierWorkflowRequest request)
    {
        if (request == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var instance = await _workflowEngine.MoveWithValidationAsync(
                id.ToString(),
                request.NextNodeId,
                UserId,
                UserRoles,
                IsAdmin,
                request.ActionLabel,
                request.Comment,
                request.NextAssigneeUserId,
                WorkflowTypeId);

            object? workflow = null;
            try { workflow = await _workflowEngine.GetInstanceStatusByEntityAsync(id.ToString(), WorkflowTypeId); }
            catch (KeyNotFoundException) { }

            return Ok(new
            {
                success = true,
                message = "Gửi duyệt lại hồ sơ thành công.",
                data = new { status = instance.Status, currentNodeName = instance.CurrentNodeName, workflow }
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MoveWorkflow(Guid id, [FromBody] MoveDossierWorkflowRequest request)
    {
        if (request == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var instance = await _workflowEngine.MoveWithValidationAsync(
                id.ToString(),
                request.NextNodeId,
                UserId,
                UserRoles,
                IsAdmin,
                request.ActionLabel,
                request.Comment,
                request.NextAssigneeUserId,
                WorkflowTypeId);

            object? workflow = null;
            try { workflow = await _workflowEngine.GetInstanceStatusByEntityAsync(id.ToString(), WorkflowTypeId); }
            catch (KeyNotFoundException) { }

            return Ok(new
            {
                success = true,
                data = new { status = instance.Status, currentNodeName = instance.CurrentNodeName, workflow }
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}/get-workflow-by-entity")]
    public async Task<IActionResult> GetWorkflowByEntity(Guid id)
    {
        var status = await _dossierWorkflowQuery.TryGetWorkflowByEntityAsync(id.ToString(), WorkflowTypeId);
        return Ok(status);
    }

    [HttpGet("{id:guid}/get-workflow-history")]
    public async Task<IActionResult> GetWorkflowHistory(Guid id)
    {
        try
        {
            var history = await _dossierWorkflowQuery.GetWorkflowHistoryAsync(id.ToString(), WorkflowTypeId);
            return Ok(history);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("get-workflow-definition/{definitionId:guid}")]
    public async Task<IActionResult> GetWorkflowDefinition(Guid definitionId)
    {
        var def = await _workflowDefinitionService.GetDefinitionByIdAsync(definitionId);
        if (def == null) return NotFound(new { message = $"Không tìm thấy định nghĩa quy trình với ID = {definitionId}" });
        return Ok(def);
    }

    [HttpGet("get-my-tasks")]
    public async Task<IActionResult> GetMyTasks([FromQuery] Guid? instanceId = null)
    {
        var tasks = await _workflowEngine.GetMyTasksAsync(UserRoles, IsAdmin, UserId, instanceId);
        return Ok(tasks);
    }

    private async Task<DossierDetailResponse?> GetDossierDetailAsync(Guid id)
    {
        try
        {
            var client = CreateEquipmentClient();
            var response = await client.GetAsync($"internal/v1/dossiers/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<DossierDetailResponse>();
        }
        catch
        {
            return null;
        }
    }

    private async Task AutoApproveDossierAsync(Guid id)
    {
        var client = CreateEquipmentClient();
        var response = await client.PostAsync($"internal/v1/dossiers/{id}/auto-approve", null);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Auto-approve thất bại ({(int)response.StatusCode}): {body}");
        }
    }

    private HttpClient CreateEquipmentClient()
    {
        var client = _httpClientFactory.CreateClient("EquipmentService");
        var internalToken = _configuration["Internal:Token"];
        if (!string.IsNullOrEmpty(internalToken))
        {
            client.DefaultRequestHeaders.Remove("X-Internal-Token");
            client.DefaultRequestHeaders.Add("X-Internal-Token", internalToken);
        }
        return client;
    }
}
