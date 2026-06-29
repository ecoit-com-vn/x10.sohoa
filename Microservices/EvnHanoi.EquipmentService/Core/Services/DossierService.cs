using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
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
    private readonly IMessageProducer _messageProducer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DossierService> _logger;

    public DossierService(
        IDossierRepository dossierRepository,
        IDossierSearchRepository dossierSearchRepository,
        IDocumentRepository documentRepository,
        IEquipmentRepository equipmentRepository,
        IMessageProducer messageProducer,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DossierService> logger)
    {
        _dossierRepository = dossierRepository ?? throw new ArgumentNullException(nameof(dossierRepository));
        _dossierSearchRepository = dossierSearchRepository ?? throw new ArgumentNullException(nameof(dossierSearchRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _equipmentRepository = equipmentRepository ?? throw new ArgumentNullException(nameof(equipmentRepository));
        _messageProducer = messageProducer ?? throw new ArgumentNullException(nameof(messageProducer));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    public async Task<(IEnumerable<EquipmentLookupItemDto> Items, int TotalCount)> GetEquipmentLookupAsync(
        EquipmentLookupFilterDto filter,
        bool isAdmin,
        long? userUnitId,
        IReadOnlyList<long>? fallbackUnitIds)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize < 1) filter.PageSize = 10;
        if (filter.PageSize > 100) filter.PageSize = 100;

        List<long>? allowedUnitIds = null;
        if (!isAdmin)
        {
            if (userUnitId.HasValue)
            {
                var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(userUnitId);
                allowedUnitIds = units.Select(u => u.Id).ToList();
            }
            else if (fallbackUnitIds != null && fallbackUnitIds.Count > 0)
            {
                var list = new List<long>();
                foreach (var fId in fallbackUnitIds)
                {
                    var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(fId);
                    list.AddRange(units.Select(u => u.Id));
                }
                allowedUnitIds = list.Distinct().ToList();
            }
            else
            {
                allowedUnitIds = new List<long> { -1 };
            }

            if (filter.UnitId.HasValue && !allowedUnitIds.Contains(filter.UnitId.Value))
            {
                return (Enumerable.Empty<EquipmentLookupItemDto>(), 0);
            }
        }

        return await _equipmentRepository.GetLookupPagedAsync(filter, allowedUnitIds);
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
        {
            var synced = await TrySyncDeleteFromEsAsync(id);
            var queued = await TryPublishDossierChangedAsync(id, DossierChangedActions.Deleted);
            if (!synced && !queued)
            {
                _logger.LogError(
                    "Dossier {DossierId} đã xóa mềm Oracle nhưng không đồng bộ được Elasticsearch.",
                    id);
            }
            else if (!synced)
            {
                _logger.LogWarning(
                    "Dossier {DossierId}: sync ES delete thất bại, đã đưa vào hàng đợi RabbitMQ.",
                    id);
            }
        }
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

    // ===== WORKFLOW SYNC (nhận đồng bộ từ WorkflowService qua API nội bộ) =====

    /// <summary>
    /// Cập nhật trạng thái workflow của hồ sơ theo dữ liệu do WorkflowService đẩy về.
    /// WorkflowService là bên SỞ HỮU logic suy ra DossierStatus; ES chỉ ghi nhận.
    /// </summary>
    public async Task UpdateWorkflowStateInternalAsync(Guid id, UpdateInternalWorkflowStateDto dto)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        var status = string.IsNullOrWhiteSpace(dto.DossierStatus) ? existing.Status : dto.DossierStatus;
        var statusName = dto.WorkflowStatusName ?? existing.WorkflowStatusName ?? string.Empty;

        await _dossierRepository.UpdateWorkflowAsync(id, dto.WorkflowInstanceId, statusName, status, "system");

        // WF đã hoàn thành (Approved) → xóa WORKFLOW_TASKS_ACTIVE; chỉ lưu khi còn bước đang chạy
        var persistActiveTask = !string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(dto.CurrentStepId);

        if (persistActiveTask)
        {
            var assignees = dto.CurrentAssignees != null && dto.CurrentAssignees.Any()
                ? string.Join(",", dto.CurrentAssignees)
                : string.Empty;

            var actionsJson = dto.AvailableActions != null && dto.AvailableActions.Any()
                ? System.Text.Json.JsonSerializer.Serialize(dto.AvailableActions)
                : "[]";

            await _dossierRepository.SaveActiveWorkflowTaskAsync(
                id,
                dto.CurrentStepId!,
                statusName,
                assignees,
                actionsJson,
                "system");
        }
        else
        {
            await _dossierRepository.SaveActiveWorkflowTaskAsync(
                id,
                string.Empty,
                statusName,
                string.Empty,
                "[]",
                "system");
        }

        var synced = await TrySyncReindexAsync(id);
        var queued = synced
            ? false
            : await TryPublishDossierChangedAsync(id, DossierChangedActions.WorkflowChanged);

        if (!synced && !queued)
        {
            throw new InvalidOperationException(
                "Đã cập nhật trạng thái hồ sơ trong Oracle nhưng không thể đồng bộ Elasticsearch. Vui lòng thử lại.");
        }

        if (!synced)
        {
            _logger.LogWarning(
                "Dossier {DossierId}: sync reindex thất bại, đã đưa vào hàng đợi RabbitMQ dossier_index_queue.",
                id);
        }
    }

    private async Task<bool> TryPublishDossierChangedAsync(Guid dossierId, string action)
    {
        try
        {
            await PublishDossierChangedAsync(dossierId, action);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể publish sự kiện index hồ sơ {DossierId} vào RabbitMQ.", dossierId);
            return false;
        }
    }

    private async Task<bool> TrySyncDeleteFromEsAsync(Guid dossierId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"internal/v1/dossiers/{dossierId}");
            var token = _configuration["Internal:Token"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError(
                    "Internal:Token chưa cấu hình — không thể gọi xóa ES đồng bộ cho hồ sơ {DossierId}.",
                    dossierId);
                return false;
            }

            request.Headers.Add("X-Internal-Token", token);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Sync delete dossier {DossierId} from ES failed ({StatusCode}): {Body}",
                    dossierId,
                    (int)response.StatusCode,
                    body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync delete dossier {DossierId} from ES failed.", dossierId);
            return false;
        }
    }

    private async Task<bool> TrySyncReindexAsync(Guid dossierId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"internal/v1/dossiers/{dossierId}/reindex");
            var token = _configuration["Internal:Token"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError(
                    "Internal:Token chưa cấu hình — không thể gọi reindex đồng bộ cho hồ sơ {DossierId}.",
                    dossierId);
                return false;
            }

            request.Headers.Add("X-Internal-Token", token);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Sync reindex dossier {DossierId} failed ({StatusCode}): {Body}",
                    dossierId,
                    (int)response.StatusCode,
                    body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync reindex dossier {DossierId} failed.", dossierId);
            return false;
        }
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
            DossierIndexIdNormalizer.Normalize(dossierId.ToString()),
            action,
            UuidHelper.NewUuid(),
            DateTime.UtcNow);

        await _messageProducer.SendMessageAsync(evt, DossierMessaging.IndexQueue);
    }
}
