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
using System.Transactions;
using Microsoft.Extensions.Configuration;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Controllers;

/// <summary>
/// Cửa ngõ API quy trình của hồ sơ — đã tách từ EquipmentService sang WorkflowService.
/// KHÔNG gắn [WorkflowEngineApi] để DynamicPermissionFilter ánh xạ về quyền DOSSIER_*
/// (DossierWorkflow => DOSSIER): Submit=>DOSSIER_CREATE, Move=>DOSSIER_MANAGE, Get*=>DOSSIER_VIEW.
/// Gọi IWorkflowEngineService in-process (không gọi HTTP api/v1/workflows/* của chính mình).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/dossiers-workflow")]
public class DossierWorkflowController : ControllerBase
{
    private const string DossierEntityType = "Dossier";

    private readonly IWorkflowEngineService _workflowEngine;
    private readonly IWorkflowDefinitionService _workflowDefinitionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IConfiguration _configuration;

    public DossierWorkflowController(
        IWorkflowEngineService workflowEngine,
        IWorkflowDefinitionService workflowDefinitionService,
        IHttpClientFactory httpClientFactory,
        IWorkflowRepository workflowRepository,
        IConfiguration configuration)
    {
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _workflowDefinitionService = workflowDefinitionService ?? throw new ArgumentNullException(nameof(workflowDefinitionService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "system";

    private List<string> UserRoles => User.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList();

    private bool IsAdmin => User.IsInRole("ADMIN") || UserRoles.Contains("ADMIN");

    // ===== THÔNG TIN BƯỚC DUYỆT KẾ TIẾP (GET next-step-info => DOSSIER_VIEW) =====

    [HttpGet("next-step-info")]
    public async Task<IActionResult> GetNextStepInfo()
    {
        try
        {
            var definition = await _workflowRepository.GetActiveDefinitionByEntityTypeAsync(DossierEntityType);
            if (definition == null)
            {
                return NotFound(new { message = "Không tìm thấy quy trình đang hoạt động cho hồ sơ." });
            }

            var steps = definition.Steps.OrderBy(s => s.Order).ToList();
            if (steps.Count < 2)
            {
                return BadRequest(new { message = "Quy trình phê duyệt phải cấu hình ít nhất 2 bước." });
            }

            // Step 1: Thường là bước khởi tạo (Order đầu tiên)
            // Step 2: Bước phê duyệt tiếp theo (Order thứ hai)
            var step2 = steps[1];

            // Tìm node tương ứng trên BPMN XML để trả về NextNodeId chính xác
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
                            // Tìm flow đi từ StartEvent
                            var flowFromStart = process.Elements(bpmn + "sequenceFlow")
                                .FirstOrDefault(f => f.Attribute("sourceRef")?.Value == startEventId);
                            var step1NodeId = flowFromStart?.Attribute("targetRef")?.Value;
                            if (!string.IsNullOrEmpty(step1NodeId))
                            {
                                // Tìm flow đi từ Step 1
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
            {
                nextNodeId = step2.Id.ToString();
            }

            return Ok(new
            {
                nextNodeId = nextNodeId,
                stepName = step2.StepName,
                requiredRole = step2.RequiredRole,
                requiresNextAssignee = !string.IsNullOrEmpty(step2.RequiredRole)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi lấy thông tin bước phê duyệt.", detail = ex.Message });
        }
    }

    // ===== GỬI DUYỆT TÍCH HỢP (POST submit => DOSSIER_CREATE) =====

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitAndMoveRequest request)
    {
        if (request == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        // 1. Kiểm tra trạng thái hồ sơ chéo
        var dossierDetail = await GetDossierDetailAsync(id);
        if (dossierDetail == null)
        {
            return BadRequest(new { message = "Không thể kết nối dịch vụ thiết bị để kiểm tra trạng thái hồ sơ." });
        }

        if (dossierDetail.StatusId != 2) // 2 = CompletedInput (Hoàn thành)
        {
            return BadRequest(new { message = $"Hồ sơ phải ở trạng thái 'Hoàn thành' mới được phép gửi duyệt. Trạng thái hiện tại: {dossierDetail.Status}" });
        }

        // 2. Thực thi Submit & Move tuần tự (không dùng TransactionScope để tránh lỗi ODP.NET Ambient Transaction)
        WorkflowInstance? instance = null;
        try
        {
            // Submit (Tạo instance đứng ở Step 1)
            instance = await _workflowEngine.SubmitByEntityTypeAsync(id.ToString(), DossierEntityType, UserId);
        }
        catch (KeyNotFoundException ex)
        {
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
            // Move (Chuyển tiếp ngầm ngay lập tức từ Step 1 sang Step 2)
            var updatedInstance = await _workflowEngine.MoveWithValidationAsync(
                id.ToString(),
                request.NextNodeId,
                UserId,
                UserRoles,
                IsAdmin,
                request.ActionLabel,
                request.Comment,
                request.NextAssigneeUserId,
                DossierEntityType);

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
            // ROLLBACK thủ công khi Move bị lỗi
            if (instance != null)
            {
                try
                {
                    // 1. Xóa Workflow Instance vật lý vừa tạo
                    await _workflowRepository.DeleteInstancePhysicalAsync(instance.Id);
                }
                catch
                {
                    // Nuốt lỗi rollback DB
                }

                try
                {
                    // 2. Gọi HTTP nội bộ chuyển trạng thái hồ sơ nghiệp vụ về lại CompletedInput
                    var client = _httpClientFactory.CreateClient("EquipmentService");
                    var internalToken = _configuration["Internal:Token"];
                    if (!string.IsNullOrEmpty(internalToken))
                    {
                        client.DefaultRequestHeaders.Remove("X-Internal-Token");
                        client.DefaultRequestHeaders.Add("X-Internal-Token", internalToken);
                    }

                    var rollbackDto = new
                    {
                        dossierStatusId = 2, // CompletedInput (Hoàn thành)
                        workflowInstanceId = (Guid?)null,
                        workflowStepName = (string?)null
                    };
                    await client.PutAsJsonAsync($"internal/v1/dossiers/{id}/workflow-state", rollbackDto);
                }
                catch
                {
                    // Nuốt lỗi rollback HTTP
                }
            }

            if (ex is KeyNotFoundException) return NotFound(new { message = ex.Message });
            if (ex is ArgumentException) return BadRequest(new { message = ex.Message });
            if (ex is InvalidOperationException) return BadRequest(new { message = ex.Message });
            
            return StatusCode(500, new { message = "Lỗi khi chuyển tiếp hồ sơ đến người duyệt tiếp theo.", detail = ex.Message });
        }
    }

    private async Task<DossierDetailResponse?> GetDossierDetailAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("EquipmentService");
            var internalToken = _configuration["Internal:Token"];
            if (!string.IsNullOrEmpty(internalToken))
            {
                client.DefaultRequestHeaders.Remove("X-Internal-Token");
                client.DefaultRequestHeaders.Add("X-Internal-Token", internalToken);
            }

            var response = await client.GetAsync($"internal/v1/dossiers/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<DossierDetailResponse>();
        }
        catch
        {
            return null;
        }
    }



    // ===== GỬI DUYỆT LẠI (POST resubmit => DOSSIER_CREATE) =====

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
                DossierEntityType);

            object? workflow = null;
            try
            {
                workflow = await _workflowEngine.GetInstanceStatusByEntityAsync(id.ToString(), DossierEntityType);
            }
            catch (KeyNotFoundException)
            {
            }

            return Ok(new
            {
                success = true,
                message = "Gửi duyệt lại hồ sơ thành công.",
                data = new
                {
                    status = instance.Status,
                    currentNodeName = instance.CurrentNodeName,
                    workflow
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ===== CHUYỂN BƯỚC (POST move => DOSSIER_MANAGE) =====

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
                DossierEntityType);

            object? workflow = null;
            try
            {
                workflow = await _workflowEngine.GetInstanceStatusByEntityAsync(id.ToString(), DossierEntityType);
            }
            catch (KeyNotFoundException)
            {
                // Instance có thể đã kết thúc — bỏ qua snapshot workflow.
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    status = instance.Status,
                    currentNodeName = instance.CurrentNodeName,
                    workflow
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ===== TRẠNG THÁI / LỊCH SỬ / TASK (GET => DOSSIER_VIEW) =====

    [HttpGet("{id:guid}/get-workflow-by-entity")]
    public async Task<IActionResult> GetWorkflowByEntity(Guid id)
    {
        try
        {
            var status = await _workflowEngine.GetInstanceStatusByEntityAsync(id.ToString(), DossierEntityType);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            // Hồ sơ chưa vào quy trình — tương thích endpoint cũ ở EquipmentService (trả 200 + null, không 404).
            return Ok(null);
        }
    }

    [HttpGet("{id:guid}/get-workflow-history")]
    public async Task<IActionResult> GetWorkflowHistory(Guid id)
    {
        try
        {
            var history = await _workflowEngine.GetHistoryByEntityAsync(id.ToString(), DossierEntityType);
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
}

/// <summary>Request chuyển bước workflow cho hồ sơ.</summary>
public class MoveDossierWorkflowRequest
{
    public string NextNodeId { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? NextAssigneeUserId { get; set; }
}

/// <summary>Request gửi duyệt và chuyển bước tích hợp.</summary>
public class SubmitAndMoveRequest
{
    public string NextNodeId { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = "Trình duyệt";
    public string? Comment { get; set; }
    public string? NextAssigneeUserId { get; set; }
}

/// <summary>DTO nhận trạng thái hồ sơ nghiệp vụ từ EquipmentService.</summary>
public class DossierDetailResponse
{
    public string Status { get; set; } = string.Empty;
    public int StatusId { get; set; }
}
