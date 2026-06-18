using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;
using System.Net.Http.Json;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public DossierService(
        IDossierRepository dossierRepository, 
        IHttpClientFactory httpClientFactory)
    {
        _dossierRepository = dossierRepository ?? throw new ArgumentNullException(nameof(dossierRepository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
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
        return await _dossierRepository.GetPagedAsync(filter);
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
            Status = DossierStatus.Draft,
            RowVersion = 1,
            CreatorId = string.IsNullOrEmpty(userId) ? null : Guid.TryParse(userId, out var uid) ? uid : null,
            CreatorUsername = userName,
            CreatorName = userFullName,
            CreatedBy = userName,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        return await _dossierRepository.CreateAsync(dossier, dto.EquipmentIds);
    }

    public async Task<bool> UpdateAsync(Guid id, DossierUpdateDto dto, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        existing.GridTypeId = dto.GridTypeId;
        existing.InfrastructureId = dto.InfrastructureId;
        existing.DossierSetId = dto.DossierSetId;
        existing.DossierTypeId = dto.DossierTypeId;
        existing.ModifiedBy = userId;
        existing.ModifiedDate = DateTime.UtcNow;
        existing.RowVersion = dto.RowVersion;

        return await _dossierRepository.UpdateAsync(existing, dto.EquipmentIds);
    }

    public async Task<bool> DeleteAsync(Guid id, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        if (existing.Status != DossierStatus.Draft && existing.Status != DossierStatus.Returned)
            throw new InvalidOperationException("Chỉ có thể xóa hồ sơ ở trạng thái Nháp hoặc Trả lại.");

        return await _dossierRepository.SoftDeleteAsync(id, userId);
    }

    // ===== FORM DATA + VERSIONING =====

    public async Task<DossierDetailDto?> SaveFormDataAsync(Guid id, DossierSaveFormDataDto dto, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

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

        return await _dossierRepository.GetDetailByIdAsync(id);
    }

    public async Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid id)
    {
        return await _dossierRepository.GetVersionsAsync(id);
    }

    // ===== EQUIPMENT MANAGEMENT =====

    public async Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid id)
    {
        return await _dossierRepository.GetEquipmentsAsync(id);
    }

    public async Task<bool> AddEquipmentAsync(Guid id, Guid equipmentId)
    {
        return await _dossierRepository.AddEquipmentAsync(id, equipmentId);
    }

    public async Task<bool> RemoveEquipmentAsync(Guid id, Guid equipmentId)
    {
        return await _dossierRepository.RemoveEquipmentAsync(id, equipmentId);
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
            EntityType = "Dossier"
        });

        if (!submitResponse.IsSuccessStatusCode)
        {
            var errorBody = await submitResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Gửi duyệt quy trình thất bại: {errorBody}");
        }

        var instance = await submitResponse.Content.ReadFromJsonAsync<WorkflowInstanceRef>();
        if (instance == null) throw new InvalidOperationException("Không nhận được kết quả khởi tạo quy trình.");

        await _dossierRepository.UpdateWorkflowAsync(
            id,
            instance.Id,
            instance.Status ?? "Đang xử lý",
            DossierStatus.PendingApproval,
            userId);

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
            NextAssigneeUserId = nextAssigneeUserId
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
        var client = _httpClientFactory.CreateClient("WorkflowService");
        var response = await client.GetAsync($"api/v1/workflows/get-workflow-by-entity/{entityId}?entityType=Dossier");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<object>();
    }

    public async Task<IEnumerable<object>> GetWorkflowHistoryAsync(Guid dossierId)
    {
        var dossier = await _dossierRepository.GetByIdAsync(dossierId);
        if (dossier == null || !dossier.WorkflowInstanceId.HasValue)
            return Enumerable.Empty<object>();

        var client = _httpClientFactory.CreateClient("WorkflowService");
        var response = await client.GetAsync($"api/v1/workflows/get-workflow-history/{dossierId}");
        if (!response.IsSuccessStatusCode) return Enumerable.Empty<object>();

        return await response.Content.ReadFromJsonAsync<IEnumerable<object>>() ?? Enumerable.Empty<object>();
    }

    public async Task<object?> GetWorkflowDefinitionAsync(Guid definitionId)
    {
        var client = _httpClientFactory.CreateClient("WorkflowService");
        var response = await client.GetAsync($"api/v1/workflows/get-workflow-definition/{definitionId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<object>();
    }

    public async Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId)
    {
        var client = _httpClientFactory.CreateClient("WorkflowService");
        var response = await client.GetAsync("api/v1/workflows/get-my-tasks");
        if (!response.IsSuccessStatusCode) return Enumerable.Empty<object>();
        return await response.Content.ReadFromJsonAsync<IEnumerable<object>>() ?? Enumerable.Empty<object>();
    }
}

/// <summary>
/// Lightweight reference model cho kết quả khởi tạo WorkflowInstance từ WorkflowService
/// </summary>
internal class WorkflowInstanceRef
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
    public string? CurrentNodeId { get; set; }
}
