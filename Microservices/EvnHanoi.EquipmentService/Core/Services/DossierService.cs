using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Messaging;
using System.Net.Http.Json;
using System.Text.Json;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;
using GridTypeEntity = EvnHanoi.EquipmentService.Core.Entities.GridType;


namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Service hồ sơ thiết bị — encapsulate toàn bộ logic nghiệp vụ và workflow.
/// Workflow operations gọi qua HTTP tới WorkflowService (microservice riêng).
/// </summary>
public class DossierService : IDossierService
{
    private readonly IDossierRepository _dossierRepository;
    private readonly IDossierSearchRepository _dossierSearchRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMessageProducer _messageProducer;

    public DossierService(
        IDossierRepository dossierRepository,
        IDossierSearchRepository dossierSearchRepository,
        IDocumentRepository documentRepository,
        IEquipmentRepository equipmentRepository,
        IHttpClientFactory httpClientFactory,
        IMessageProducer messageProducer)
    {
        _dossierRepository = dossierRepository ?? throw new ArgumentNullException(nameof(dossierRepository));
        _dossierSearchRepository = dossierSearchRepository ?? throw new ArgumentNullException(nameof(dossierSearchRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _equipmentRepository = equipmentRepository ?? throw new ArgumentNullException(nameof(equipmentRepository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _messageProducer = messageProducer ?? throw new ArgumentNullException(nameof(messageProducer));
    }

    // ====loookup ====

    public async Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync()
    {
        return await _dossierRepository.GetInfrastructuresLookupAsync();
    }

    public async Task<IEnumerable<GridTypeEntity>> GetGridTypesLookupAsync()
    {
        return await _dossierRepository.GetGridTypesLookupAsync();
    }
    public async Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync()
    {
        return await _dossierRepository.GetDossierTypesLookupAsync();
    }



    // ===== CRUD =====

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter)
    {
        if (filter.UnitId.HasValue)
        {
            var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(filter.UnitId);
            filter.UnitScopeIds = units.Select(u => u.Id).Distinct().ToList();
        }

        return await _dossierSearchRepository.GetPagedAsync(filter);
    }

    public async Task<DossierDetailDto?> GetDetailByIdAsync(Guid id)
    {
        return await _dossierRepository.GetDetailByIdAsync(id);
    }

    public async Task<Guid> CreateAsync(DossierCreateDto dto, string userId, string userName, string userFullName)
    {
        var dossier = new Dossier
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            GridTypeId = dto.GridTypeId,
            InfrastructureId = dto.InfrastructureId,
            DossierSetId = dto.DossierSetId,
            DossierTypeId = dto.DossierTypeId,
            FormDataJson = dto.FormDataJson,
            Status = DossierStatus.Draft,
            RowVersion = 1,
            CreatorId = string.IsNullOrEmpty(userId) ? null : Guid.TryParse(userId, out var uid) ? uid : null,
            CreatorUsername = userName,
            CreatorName = userFullName,
            CreatedBy = userName,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        var newId = await _dossierRepository.CreateAsync(dossier, dto.EquipmentIds);

        // Tạo phiên bản khởi đầu (v1) ngay khi hồ sơ được tạo mới
        if (!string.IsNullOrEmpty(dto.FormDataJson))
        {
            var firstVersion = new DossierVersion
            {
                DossierId = newId,
                FormDataJson = dto.FormDataJson,
                ChangeNote = "Khởi tạo hồ sơ",
                CreatedBy = userName,
                CreatedDate = DateTime.UtcNow
            };
            await _dossierRepository.CreateVersionAsync(firstVersion);
        }

        await PublishDossierChangedAsync(newId, DossierChangedActions.Created);
        return newId;
    }

    public async Task<bool> UpdateAsync(Guid id, DossierUpdateDto dto, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        if (!string.IsNullOrEmpty(dto.FormDataJson))
            await EnsureCanEditFormDataAsync(existing);

        existing.GridTypeId = dto.GridTypeId;
        existing.InfrastructureId = dto.InfrastructureId;
        existing.DossierSetId = dto.DossierSetId;
        existing.DossierTypeId = dto.DossierTypeId;
        existing.FormDataJson = dto.FormDataJson ?? existing.FormDataJson;
        existing.ModifiedBy = userId;
        existing.ModifiedDate = DateTime.UtcNow;
        existing.RowVersion = dto.RowVersion;

        var updated = await _dossierRepository.UpdateAsync(existing, dto.EquipmentIds);
        if (updated)
            await PublishDossierChangedAsync(id, DossierChangedActions.Updated);
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        var isDraft = existing.Status == DossierStatus.Draft;
        var notInWorkflow = !existing.WorkflowInstanceId.HasValue;
        if (!isDraft && !notInWorkflow)
            throw new InvalidOperationException("Chỉ có thể xóa hồ sơ ở trạng thái Nháp hoặc chưa đưa vào quy trình phê duyệt.");

        var deleted = await _dossierRepository.SoftDeleteAsync(id, userId);
        if (deleted)
            await PublishDossierChangedAsync(id, DossierChangedActions.Deleted);
        return deleted;
    }

    // ===== FORM DATA + VERSIONING =====

    public async Task<DossierDetailDto?> SaveFormDataAsync(Guid id, DossierSaveFormDataDto dto, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        await EnsureCanEditFormDataAsync(existing);

        // Cập nhật FormDataJson với Optimistic Locking
        await _dossierRepository.UpdateFormDataAsync(id, dto.FormDataJson, dto.RowVersion, userId);

        // Tạo snapshot version
        var version = new DossierVersion
        {
            DossierId = id,
            FormDataJson = dto.FormDataJson,
            ChangeNote = dto.ChangeNote,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };
        await _dossierRepository.CreateVersionAsync(version);

        await PublishDossierChangedAsync(id, DossierChangedActions.FormDataSaved);
        return await _dossierRepository.GetDetailByIdAsync(id);
    }

    public async Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid id)
    {
        return await _dossierRepository.GetVersionsAsync(id);
    }

    public async Task EnsureCanEditFormDataAsync(Guid dossierId)
    {
        var existing = await _dossierRepository.GetByIdAsync(dossierId);
        if (existing == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {dossierId}");
        await EnsureCanEditFormDataAsync(existing);
    }

    public async Task RecordDocumentListChangeAsync(Guid dossierId, string changeNote, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(dossierId);
        if (existing == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {dossierId}");

        var filter = new DossierDocumentFilterDto { Page = 1, PageSize = 1000 };
        var (items, _) = await _documentRepository.GetDocumentsByDossierAsync(dossierId, filter);
        var snapshot = System.Text.Json.JsonSerializer.Serialize(
            items.Select(d => new DossierDocumentSnapshotItemDto
            {
                Id = d.Id,
                Name = d.Name,
                FileSize = d.FileSize,
                MimeType = d.MimeType,
                LatestVersionId = d.LatestVersionId
            }));

        var version = new DossierVersion
        {
            DossierId = dossierId,
            FormDataJson = existing.FormDataJson,
            DocumentsSnapshotJson = snapshot,
            ChangeNote = changeNote,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };
        await _dossierRepository.CreateVersionAsync(version);
        await PublishDossierChangedAsync(dossierId, DossierChangedActions.Updated);
    }

    // ===== EQUIPMENT MANAGEMENT =====

    public async Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid id)
    {
        return await _dossierRepository.GetEquipmentsAsync(id);
    }

    public async Task<bool> AddEquipmentAsync(Guid id, Guid equipmentId)
    {
        var added = await _dossierRepository.AddEquipmentAsync(id, equipmentId);
        if (added)
            await PublishDossierChangedAsync(id, DossierChangedActions.Updated);
        return added;
    }

    public async Task<bool> RemoveEquipmentAsync(Guid id, Guid equipmentId)
    {
        var removed = await _dossierRepository.RemoveEquipmentAsync(id, equipmentId);
        if (removed)
            await PublishDossierChangedAsync(id, DossierChangedActions.Updated);
        return removed;
    }

    // ===== WORKFLOW OPERATIONS (gọi qua HTTP tới WorkflowService) =====

    /// <summary>
    /// Gửi duyệt hồ sơ — chỉ khi Status = Draft hoặc Returned.
    /// Token Relay: JWT của user được tự động đính kèm bởi TokenRelayHandler.
    /// WorkflowService tự tìm definition active theo EntityType = "Dossier".
    /// </summary>
    public async Task<DossierDetailDto?> SubmitForApprovalAsync(Guid id, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        if (existing.Status != DossierStatus.Draft && existing.Status != DossierStatus.Returned)
            throw new InvalidOperationException("Chỉ có thể gửi duyệt hồ sơ ở trạng thái Nháp hoặc Trả lại.");

        var client = _httpClientFactory.CreateClient("WorkflowService");

        var submitResponse = await client.PostAsJsonAsync("api/v1/workflows/submit", new
        {
            EntityId = id.ToString(),
            // EntityType = description của enum WorkflowType → tìm đúng WorkflowDefinition
            EntityType = "Quy trình số hóa hồ sơ",
            // TargetEntityType → gắn vào WorkflowInstance để query lại sau (GET by entity)
            TargetEntityType = "Dossier"
        });

        if (!submitResponse.IsSuccessStatusCode)
        {
            var errorBody = await submitResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Gửi duyệt quy trình thất bại: {errorBody}");
        }

        var instance = await submitResponse.Content.ReadFromJsonAsync<WorkflowInstanceRef>();
        if (instance == null || instance.InstanceId == Guid.Empty)
            throw new InvalidOperationException("Không nhận được instanceId hợp lệ từ WorkflowService.");

        await _dossierRepository.UpdateWorkflowAsync(
            id,
            instance.InstanceId,
            instance.Status ?? "Đang xử lý",
            DossierStatus.PendingApproval,
            userId);

        await PublishDossierChangedAsync(id, DossierChangedActions.WorkflowChanged);
        return await _dossierRepository.GetDetailByIdAsync(id);
    }

    public async Task<object?> MoveWorkflowAsync(string dossierId, string nextNodeId, string userId, string actionLabel, string? comment, string? nextAssigneeUserId = null)
    {
        var client = _httpClientFactory.CreateClient("WorkflowService");
        var response = await client.PostAsJsonAsync("api/v1/workflows/move", new
        {
            DossierId = dossierId,
            NextNodeId = nextNodeId,
            ActionLabel = actionLabel,
            Comment = comment,
            NextAssigneeUserId = nextAssigneeUserId,
            EntityType = "Dossier"
        });

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Chuyển bước quy trình thất bại: {error}");
        }

        return await response.Content.ReadFromJsonAsync<object>();
    }

    public async Task<object?> GetWorkflowStatusByEntityAsync(string entityId)
    {
        return await _dossierRepository.GetWorkflowStatusByEntityAsync(entityId);
    }

    public async Task<IEnumerable<object>> GetWorkflowHistoryAsync(Guid dossierId)
    {
        var client = _httpClientFactory.CreateClient("WorkflowService");
        // WorkflowController.GetWorkflowHistory trả về IEnumerable<WorkflowHistory> (array JSON)
        var response = await client.GetAsync($"api/v1/workflows/get-workflow-history/{dossierId}?entityType=Dossier");
        if (!response.IsSuccessStatusCode) return Enumerable.Empty<object>();

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content)) return Enumerable.Empty<object>();

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
            return root.Deserialize<IEnumerable<object>>() ?? Enumerable.Empty<object>();

        return Enumerable.Empty<object>();
    }

    public async Task<object?> GetWorkflowDefinitionAsync(Guid definitionId)
    {
        var client = _httpClientFactory.CreateClient("WorkflowService");
        var response = await client.GetAsync($"api/v1/workflows/get-workflow-definition/{definitionId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<object>();
    }

    public async Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId, Guid? workflowInstanceId = null)
    {
        var client = _httpClientFactory.CreateClient("WorkflowService");
        var url = "api/v1/workflows/get-my-tasks";
        if (workflowInstanceId.HasValue)
            url += $"?instanceId={workflowInstanceId.Value}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode) return Enumerable.Empty<object>();
        return await response.Content.ReadFromJsonAsync<IEnumerable<object>>() ?? Enumerable.Empty<object>();
    }

    /// <summary>
    /// Kiểm tra quyền sửa dữ liệu hồ sơ:
    /// - Chưa vào workflow: chỉ Draft hoặc Returned.
    /// - Đã vào workflow: bước hiện tại phải có AllowEdit = true và instance đang Running.
    /// </summary>
    private async Task EnsureCanEditFormDataAsync(Dossier dossier)
    {
        if (!dossier.WorkflowInstanceId.HasValue)
        {
            if (dossier.Status != DossierStatus.Draft && dossier.Status != DossierStatus.Returned)
                throw new InvalidOperationException("Không thể chỉnh sửa dữ liệu hồ sơ ở trạng thái hiện tại.");
            return;
        }

        var statusDto = await _dossierRepository.GetWorkflowStatusByEntityAsync(dossier.Id.ToString());
        if (statusDto == null)
            throw new InvalidOperationException("Không thể xác minh quyền chỉnh sửa theo quy trình phê duyệt.");

        if (!string.Equals(statusDto.Status, "Running", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Quy trình phê duyệt đã kết thúc, không thể chỉnh sửa dữ liệu hồ sơ.");

        if (!statusDto.CurrentStepAllowEdit)
            throw new InvalidOperationException("Bước hiện tại của quy trình không cho phép chỉnh sửa dữ liệu hồ sơ.");
    }

    private async Task PublishDossierChangedAsync(Guid dossierId, string action)
    {
        var evt = new DossierChangedEvent(
            dossierId.ToString(),
            action,
            UuidHelper.NewUuid(),
            DateTime.UtcNow);

        await _messageProducer.SendMessageAsync(evt, DossierMessaging.IndexQueue);
    }
}

/// <summary>
/// Maps response của POST api/v1/workflows/submit:
/// { "success", "message", "instanceId", "status" }
/// </summary>
internal class WorkflowInstanceRef
{
    /// <summary>InstanceId từ response body (camelCase: instanceId)</summary>
    public Guid InstanceId { get; set; }
    public string? Status { get; set; }
    public string? CurrentNodeId { get; set; }
}
